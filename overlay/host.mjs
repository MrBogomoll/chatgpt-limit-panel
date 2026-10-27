import { spawn } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { RateLimitReader } from '../plugins/quota-chips/server/reader.mjs';
import { normalizeQuota } from '../plugins/quota-chips/server/quota.mjs';
import { overlayView } from './model.mjs';
import {createUpdateChecker,CURRENT_VERSION} from '../plugins/quota-panel/server/updates.mjs';

const executablePath = fileURLToPath(new URL('../dist/overlay/QuotaOverlay.exe', import.meta.url));
const defaultStatePath = () => join(process.env.LOCALAPPDATA || join(homedir(), 'AppData', 'Local'), 'ChatGPTQuotaOverlay', 'state.json');

/** Own the GUI and read-only quota reader together; neither survives the other. */
export function createOverlayHost({
  reader = new RateLimitReader(),
  guiCommand = executablePath,
  guiArgs = [],
  statePath = defaultStatePath(),
  pollIntervalMs = 30_000,
  freshnessIntervalMs = 1_000,
  shutdownTimeoutMs = 1_000,
  waitForApp = false,
  now = Date.now,
  timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone,
  spawnGui = spawn,
  updateChecker=createUpdateChecker(),
} = {}) {
  const absoluteStatePath = resolve(statePath);
  let child, lines, pollTimer, freshnessTimer, shutdownTimer, inFlight;
  let ready = false, paused = false, stopping = false, closed = false;
  let appPresent = !waitForApp;
  let taskbarEnabled = false;
  let checkingUpdate=false;
  let snapshot = normalizeQuota(null, now());
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
    clearInterval(pollTimer);
    clearInterval(freshnessTimer);
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
    const view = overlayView(snapshot, { now: now(), timeZone });
    try {
      child.stdin.write(JSON.stringify(view) + '\n', error => {
        if (error) stop(1, 'gui-input-error');
      });
    } catch { stop(1, 'gui-input-error'); }
  }
  function sendUpdate(update) {
    if(stopping||!child||child.stdin.destroyed)return;
    try{child.stdin.write(JSON.stringify({update})+'\n',error=>{if(error)stop(1,'gui-input-error');});}catch{stop(1,'gui-input-error');}
  }
  async function checkUpdates() {
    if(checkingUpdate||stopping)return;
    checkingUpdate=true;sendUpdate({state:'checking',currentVersion:CURRENT_VERSION});
    try{sendUpdate(await updateChecker.check());}
    catch{sendUpdate({state:'error',reason:'network',currentVersion:CURRENT_VERSION});}
    finally{checkingUpdate=false;}
  }

  function refresh() {
    if (stopping || !ready || paused || (!appPresent && !taskbarEnabled)) return Promise.resolve(null);
    if (inFlight) return inFlight;
    // Set the shared promise before calling even a synchronous injected reader.
    const pending = Promise.resolve().then(async () => {
      if (stopping || paused || (!appPresent && !taskbarEnabled)) return;
      const raw = await reader.read();
      if (!stopping) snapshot = normalizeQuota(raw, now());
    }).catch(() => {
      if (!stopping) snapshot = { ...snapshot, stale: true };
    }).then(() => {
      if (!stopping) render();
      return snapshot;
    }).finally(() => {
      if (inFlight === pending) inFlight = null;
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
        pollTimer = setInterval(refresh, pollIntervalMs);
        refresh();
        break;
      case 'refresh': refresh(); break;
      case 'check-updates': checkUpdates(); break;
      case 'app-open': if(!appPresent){appPresent=true;refresh();} break;
      case 'app-closed': if(appPresent){appPresent=false;if(waitForApp&&!taskbarEnabled)closeReader();} break;
      case 'taskbar-on': if(!taskbarEnabled){taskbarEnabled=true;refresh();} break;
      case 'taskbar-off': if(taskbarEnabled){taskbarEnabled=false;if(waitForApp&&!appPresent)closeReader();} break;
      case 'pause': paused = true; break;
      case 'resume':
        if (paused) { paused = false; refresh(); }
        break;
      case 'exit': stop(0, 'gui-exit'); break;
    }
  }

  try {
    mkdirSync(dirname(absoluteStatePath), { recursive: true });
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
  const host = createOverlayHost({ statePath,waitForApp:true });
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
