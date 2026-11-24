import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, renameSync, unlinkSync, writeFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { createInterface } from 'node:readline';
import { setTimeout as delay } from 'node:timers/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { RateLimitReader, RateLimitReadError } from '../plugins/chatgpt-limit-panel/server/reader.mjs';
import { normalizeQuota } from '../plugins/chatgpt-limit-panel/server/limits.mjs';
import { overlayView } from './model.mjs';
import {createUpdateChecker,CURRENT_VERSION} from '../plugins/chatgpt-limit-panel/server/updates.mjs';
import {launchPanelUpdate,managedUpdateRoot,takeUpdateOutcome} from './updater.mjs';

const executablePath = fileURLToPath(new URL('../dist/overlay/ChatGPTLimitPanel.exe', import.meta.url));
const failureHistoryLimit = 10;
const attemptHistoryLimit = 8;
const failureReasons = new Set(['empty-reply', 'incomplete-reply', 'timeout', 'rejected', 'disconnected', 'unavailable', 'read-failed']);
const failureStages = new Set(['launch', 'initialize', 'read-limits', 'validate-reply', 'unknown']);
const isTimestamp = value => typeof value === 'string' && /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$/.test(value);

function safeAttempt({ at, stage, reason, durationMs }) {
  return {
    at,
    stage: failureStages.has(stage) ? stage : 'unknown',
    reason: failureReasons.has(reason) ? reason : 'read-failed',
    ...(Number.isSafeInteger(durationMs) && durationMs >= 0 ? { durationMs } : {}),
  };
}

function createRefreshLog(file, now) {
  let incidents = [];
  let active = null;
  const temporary = `${file}.${process.pid}.tmp`;

  function save() {
    try {
      writeFileSync(temporary, JSON.stringify(incidents, null, 2) + '\n', 'utf8');
      renameSync(temporary, file);
    } catch {
      // Diagnosis is best effort. A locked file must not interrupt quota reads.
      try { unlinkSync(temporary); } catch {}
    }
  }

  try {
    const stored = JSON.parse(readFileSync(file, 'utf8'));
    if (Array.isArray(stored)) {
      for (const entry of stored.slice(-failureHistoryLimit)) {
        if (!isTimestamp(entry?.startedAt) || !isTimestamp(entry.lastFailedAt) ||
            !Number.isSafeInteger(entry.attempts) || entry.attempts < 1 ||
            !Array.isArray(entry.reasons)) continue;
        const reasons = [...new Set(entry.reasons.filter(value => failureReasons.has(value)))];
        if (!reasons.length) continue;
        const recentAttempts = Array.isArray(entry.recentAttempts)
          ? entry.recentAttempts.slice(-attemptHistoryLimit).filter(attempt =>
            isTimestamp(attempt?.at) && failureStages.has(attempt.stage) && failureReasons.has(attempt.reason)).map(safeAttempt)
          : [];
        incidents.push({
          startedAt: entry.startedAt, lastFailedAt: entry.lastFailedAt,
          recoveredAt: isTimestamp(entry.recoveredAt) ? entry.recoveredAt : null,
          attempts: entry.attempts, reasons, markedStale: entry.markedStale === true,
          ...(recentAttempts.length ? { recentAttempts } : {}),
        });
      }
    }
  } catch (error) {
    if (error.code === 'ENOENT') save();
  }

  return function record(failure, markedStale) {
    if (!failure && !active) return;
    const at = new Date(now()).toISOString();
    if (!failure) {
      active.recoveredAt = at;
      active = null;
    } else {
      if (!active) {
        active = { startedAt: at, lastFailedAt: at, recoveredAt: null, attempts: 0, reasons: [], markedStale: false, recentAttempts: [] };
        incidents.push(active);
        if (incidents.length > failureHistoryLimit) incidents.shift();
      }
      active.lastFailedAt = at;
      active.attempts++;
      const attempt = safeAttempt({ ...failure, at });
      if (!active.reasons.includes(attempt.reason)) active.reasons.push(attempt.reason);
      active.recentAttempts.push(attempt);
      if (active.recentAttempts.length > attemptHistoryLimit) active.recentAttempts.shift();
      if (markedStale) active.markedStale = true;
    }
    save();
  };
}

export function mergeQuotaSnapshot(previous,next) {
  const planChanged = previous.planType && next.planType && previous.planType !== next.planType;
  if (planChanged) return next;
  if (next.status === 'unavailable' && previous.status !== 'unavailable') {
    return { ...previous, stale: true };
  }
  let retained=false;
  const windows=next.windows.map(window=>{
    const old=previous.windows?.find(value=>value.kind===window.kind);
    if(window.availability==='unknown' && old?.availability==='unlimited') {retained=true;return old;}
    return window;
  });
  return retained ? {...next,windows,stale:true,planType:next.planType??previous.planType} : next;
}
const defaultStatePath = () => {
  const local=process.env.LOCALAPPDATA || join(homedir(),'AppData','Local'),current=join(local,'ChatGPTLimitPanel'),legacy=join(local,'ChatGPTQuotaOverlay');
  return join(!existsSync(current)&&existsSync(legacy)?legacy:current,'state.json');
};

/** Own the GUI and read-only quota reader together; neither survives the other. */
export function createOverlayHost({
  reader = new RateLimitReader(),
  guiCommand = executablePath,
  guiArgs = [],
  statePath = defaultStatePath(),
  pollIntervalMs = 30_000,
  recoveryIntervalMs = 5_000,
  retryDelayMs = 2_000,
  freshnessIntervalMs = 1_000,
  shutdownTimeoutMs = 1_000,
  waitForApp = false,
  now = Date.now,
  timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone,
  spawnGui = spawn,
  updateChecker=createUpdateChecker({includeInstallAssets:true}),
  updateInstaller = null,
  updateCheckDelayMs = 2_000,
  updateCheckIntervalMs = 6 * 60 * 60_000,
} = {}) {
  const absoluteStatePath = resolve(statePath);
  let child, lines, pollTimer, freshnessTimer, shutdownTimer, updateTimer, inFlight, recordRefresh;
  let ready = false, paused = false, stopping = false, closed = false;
  let reconnectPending = false;
  let appPresent = !waitForApp;
  let taskbarEnabled = false;
  let liveTrayEnabled = false;
  let checkingUpdate=false, showUpdateResult=false;
  let installingUpdate=false, offeredUpdate=null;
  let lastRendered;
  let snapshot = normalizeQuota(null, now());
  const retryWait = new AbortController();
  let result = { code: 0, reason: 'stopped' };
  let resolveDone;
  const done = new Promise(resolve => { resolveDone = resolve; });

  function closeReader() {
    try { reader.close(); } catch { /* Shutdown must also work after provider failures. */ }
  }

  function stop(code = 0, reason = 'stopped') {
    if (stopping) return done;
    stopping = true;
    result = { code, reason };
    clearTimeout(pollTimer);
    clearInterval(freshnessTimer);
    clearTimeout(updateTimer);
    retryWait.abort();
    try{updateChecker.cancel?.();}catch{}
    closeReader();
    if (!child || closed) {
      resolveDone(result);
      return done;
    }
    // EOF asks WPF to close on its dispatcher; a wedged GUI still has a bounded lifetime.
    try { child.stdin.end(); } catch { /* Its close/error handler owns the outcome. */ }
    shutdownTimer = setTimeout(() => {
      if (!closed) {
        try { child.kill(); } catch { /* Already exited. */ }
      }
    }, shutdownTimeoutMs);
    return done;
  }

  function render() {
    if (stopping || !child || child.stdin.destroyed || child.stdin.writableNeedDrain) return;
    const serialized = JSON.stringify(overlayView(snapshot, { now: now(), timeZone }));
    if (serialized === lastRendered) return;
    try {
      child.stdin.write(serialized + '\n', error => {
        if (error) stop(1, 'gui-input-error');
      });
      lastRendered = serialized;
    } catch { stop(1, 'gui-input-error'); }
  }
  function sendUpdate(update) {
    if(stopping||!child||child.stdin.destroyed)return;
    try{child.stdin.write(JSON.stringify({update})+'\n',error=>{if(error)stop(1,'gui-input-error');});}catch{stop(1,'gui-input-error');}
  }
  function scheduleUpdateCheck(delayMs) {
    if (!ready || stopping || updateCheckIntervalMs <= 0) return;
    clearTimeout(updateTimer);
    updateTimer = setTimeout(() => checkUpdates(false), delayMs);
  }
  async function checkUpdates(manual) {
    if (stopping) return;
    if (manual && !showUpdateResult) {
      showUpdateResult = true;
      sendUpdate({state:'checking',currentVersion:CURRENT_VERSION});
    }
    if (checkingUpdate || stopping) return;
    clearTimeout(updateTimer);
    checkingUpdate = true;
    let result;
    try { result = await updateChecker.check(); }
    catch { result = {state:'error',reason:'network',currentVersion:CURRENT_VERSION}; }
    const silent = !showUpdateResult;
    checkingUpdate = showUpdateResult = false;
    const {installAssets,...uiResult}=result;
    if(result.state==='available')offeredUpdate=installAssets&&updateInstaller?uiResult:null;
    else if(result.state==='current'||result.state==='ahead')offeredUpdate=null;
    sendUpdate({...uiResult,canInstall:!!(result.state==='available'&&offeredUpdate),silent});
    scheduleUpdateCheck(updateCheckIntervalMs);
  }
  async function installUpdate() {
    if(stopping||!ready||installingUpdate||!offeredUpdate)return;
    installingUpdate=true;
    const request=offeredUpdate;
    sendUpdate({state:'installing'});
    try {await updateInstaller(request.latestVersion);}
    catch {sendUpdate({...request,state:'install-failed'});}
    finally{installingUpdate=false;}
  }

  function canRefresh() {
    return !stopping && ready && !paused && (appPresent || taskbarEnabled || liveTrayEnabled);
  }

  async function readSnapshot(previous) {
    let validationStartedAt;
    try {
      const raw = await reader.read();
      validationStartedAt = performance.now();
      const received = normalizeQuota(raw, now());
      const merged = mergeQuotaSnapshot(previous, received);
      if (merged.stale || received.status === 'unavailable') {
        const refreshIssue = received.status === 'unavailable' ? 'empty-reply' : 'incomplete-reply';
        return {
          next: { ...merged, refreshIssue },
          failure: { reason: refreshIssue, stage: 'validate-reply', durationMs: Math.round(performance.now() - validationStartedAt) },
        };
      }
      return { next: merged };
    } catch (error) {
      // Only fixed categories go into the local panel state.
      let failure = { reason: 'read-failed', stage: 'unknown' };
      if (validationStartedAt !== undefined) {
        failure = { ...failure, stage: 'validate-reply', durationMs: Math.round(performance.now() - validationStartedAt) };
      } else if (error instanceof RateLimitReadError) {
        const { reason, stage, durationMs } = error;
        failure = { reason: failureReasons.has(reason) ? reason : 'read-failed', stage, durationMs };
      } else {
        if (error?.message === 'Codex quota request timeout') failure.reason = 'timeout';
        if (error?.message === 'Codex quota request was rejected') failure.reason = 'rejected';
        if (error?.message === 'Codex App Server disconnected') failure.reason = 'disconnected';
      }
      return { next: { ...previous, stale: true, refreshIssue: failure.reason }, failure };
    }
  }

  function refresh() {
    if (!canRefresh()) return Promise.resolve(null);
    if (inFlight) return inFlight;
    clearTimeout(pollTimer);
    // Set the shared promise before calling even a synchronous injected reader.
    const pending = Promise.resolve().then(async () => {
      if (!canRefresh()) return;
      const previous = snapshot;
      let { next, failure } = await readSnapshot(previous);
      if (!canRefresh()) return;
      const retry = next.stale && !previous.stale;
      recordRefresh(failure, next.stale && !retry);
      if (retry) {
        // A brief outage gets one extra attempt. Once stale, the regular poll
        // handles recovery without doubling requests throughout an outage.
        try {
          await delay(retryDelayMs, undefined, { signal: retryWait.signal });
        } catch (error) {
          if (error.name !== 'AbortError') throw error;
          return;
        }
        if (!canRefresh()) return;
        ({ next, failure } = await readSnapshot(previous));
        if (!canRefresh()) return;
        recordRefresh(failure, next.stale);
      }
      snapshot = next;
    }).then(() => {
      if (!stopping) render();
      return snapshot;
    }).finally(() => {
      if (inFlight === pending) inFlight = null;
      const reconnect = reconnectPending;
      reconnectPending = false;
      if (!canRefresh()) return;
      const interval = snapshot.stale ? Math.min(pollIntervalMs, recoveryIntervalMs) : pollIntervalMs;
      pollTimer = setTimeout(refresh, reconnect && snapshot.stale ? 0 : interval);
    });
    inFlight = pending;
    return pending;
  }

  function receive(line) {
    if (stopping) return;
    let message;
    try { message = JSON.parse(line); } catch { return; }
    switch (message?.command) {
      case 'ready':
        if (ready) return;
        ready = true;
        refresh();
        scheduleUpdateCheck(updateCheckDelayMs);
        if(updateInstaller)void takeUpdateOutcome(absoluteStatePath).then(outcome=>{
          if(outcome==='restored')sendUpdate({state:'install-restored'});
        });
        break;
      case 'refresh': refresh(); break;
      case 'network-available':
        if (!canRefresh()) break;
        if (inFlight) reconnectPending = true;
        else refresh();
        break;
      case 'check-updates': checkUpdates(true); break;
      case 'install-update': installUpdate(); break;
      case 'app-open': if(!appPresent){appPresent=true;refresh();} break;
      case 'app-closed': if(appPresent){appPresent=false;if(waitForApp&&!taskbarEnabled&&!liveTrayEnabled)closeReader();} break;
      case 'taskbar-on': if(!taskbarEnabled){taskbarEnabled=true;refresh();} break;
      case 'taskbar-off': if(taskbarEnabled){taskbarEnabled=false;if(waitForApp&&!appPresent&&!liveTrayEnabled)closeReader();} break;
      case 'tray-live-on': if(!liveTrayEnabled){liveTrayEnabled=true;refresh();} break;
      case 'tray-live-off': if(liveTrayEnabled){liveTrayEnabled=false;if(waitForApp&&!appPresent&&!taskbarEnabled)closeReader();} break;
      case 'pause': paused = true; break;
      case 'resume':
        if (paused) { paused = false; refresh(); }
        break;
      case 'exit': stop(0, 'gui-exit'); break;
    }
  }

  try {
    mkdirSync(dirname(absoluteStatePath), { recursive: true });
    recordRefresh = createRefreshLog(join(dirname(absoluteStatePath), 'quota-failures.json'), now);
    child = spawnGui(guiCommand, [...guiArgs, absoluteStatePath], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    child.stdin.on('error', () => stop(1, 'gui-input-error'));
    child.stdin.on('close', () => { if (!stopping) stop(1, 'gui-input-error'); });
    child.on('error', () => stop(1, 'gui-error'));
    child.on('exit', code => {
      if (!stopping) stop(code === 0 ? 0 : 1, 'gui-exit');
    });
    child.on('close', code => {
      closed = true;
      if (!stopping) stop(code === 0 ? 0 : 1, 'gui-exit');
      clearTimeout(shutdownTimer);
      lines?.close();
      resolveDone(result);
    });
    child.stderr.resume(); // Native/backend diagnostics never become quota data or user output.
    lines = createInterface({ input: child.stdout });
    lines.on('line', receive);
    render(); // Unknown is visible while initialization is pending.
    // This clock is independent of refresh(), including a stalled provider request.
    freshnessTimer = setInterval(render, freshnessIntervalMs);
  } catch {
    stop(1, 'gui-error');
  }

  return { done, refresh, stop, statePath: absoluteStatePath, get child() { return child; }, get paused() { return paused; }, get appPresent(){return appPresent;}, get taskbarEnabled(){return taskbarEnabled;} };
}

async function run() {
  let statePath;
  const args = process.argv.slice(2);
  if (args.length) {
    if (args.length !== 2 || args[0] !== '--state') {
      process.stderr.write('Usage: node overlay/host.mjs [--state <state.json>]\n');
      process.exitCode = 1;
      return;
    }
    statePath = args[1];
  }
  statePath ??= defaultStatePath();
  const root=fileURLToPath(new URL('../',import.meta.url));
  const updateInstaller=managedUpdateRoot(root)&&process.env.QUOTA_CODEX_BIN
    ? version=>launchPanelUpdate({root,statePath,version}) : null;
  const host = createOverlayHost({statePath,waitForApp:true,updateInstaller});
  const stop = () => { host.stop(); };
  process.once('SIGINT', stop);
  process.once('SIGTERM', stop);
  const result = await host.done;
  process.removeListener('SIGINT', stop);
  process.removeListener('SIGTERM', stop);
  process.exitCode = result.code;
  if (result.code) process.stderr.write('Quota overlay could not run. Rebuild it and try again.\n');
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) await run();
