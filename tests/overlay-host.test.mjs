import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, writeFile, appendFile, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';
import { RateLimitReader } from '../plugins/chatgpt-limit-panel/server/reader.mjs';

const api = await import('../overlay/host.mjs').catch(() => ({}));
const fixture = fileURLToPath(new URL('./fixtures/overlay-gui.cjs', import.meta.url));
const appServerFixture = fileURLToPath(new URL('./fixtures/limits-app-server.cjs', import.meta.url));
const instant = Date.parse('2026-10-23T14:00:00Z');
const quota = { rateLimits: { limitId: 'codex', primary: { usedPercent: 62, windowDurationMins: 300, resetsAt: 1792774531 }, secondary: { usedPercent: 75, windowDurationMins: 10080, resetsAt: 1793297434 } } };

function readerFor(read = () => quota) {
  return { calls: 0, closes: 0, read() { this.calls++; return read(this.calls); }, close() { this.closes++; } };
}

async function until(predicate, message = 'condition', timeout = 3000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const value = await predicate();
    if (value) return value;
    await delay(10);
  }
  assert.fail(`Timed out waiting for ${message}`);
}

async function fixtureHost(t, { reader = readerFor(), config = {}, ...options } = {}) {
  assert.equal(typeof api.createOverlayHost, 'function', 'overlay host must expose its lifecycle controller');
  const directory = await mkdtemp(join(tmpdir(), 'quota overlay тест '));
  const configuredStatePath = join(directory, 'state.json');
  await writeFile(configuredStatePath + '.fixture.json', JSON.stringify(config));
  const host = api.createOverlayHost({ reader, guiCommand: process.execPath, guiArgs: [fixture], statePath: configuredStatePath, pollIntervalMs: 10000, freshnessIntervalMs: 20, shutdownTimeoutMs: 100, now: () => instant, timeZone: 'Europe/Moscow', ...options });
  const events = async () => {
    let contents;
    try { contents = await readFile(configuredStatePath + '.events.ndjson', 'utf8'); }
    catch { return []; }
    return contents.split('\n').filter(Boolean).flatMap(line => { try { return [JSON.parse(line)]; } catch { return []; } });
  };
  const views = async () => (await events()).filter(event => event.event === 'view').map(event => event.view);
  const send = command => appendFile(configuredStatePath + '.commands.ndjson', JSON.stringify({ command }) + '\n');
  t.after(async () => { await host.stop(); await rm(directory, { recursive: true, force: true }); });
  await until(async () => (await events()).some(event => event.event === 'started'), 'fixture startup');
  return { host, reader, events, views, send, statePath: configuredStatePath };
}

test('host sends explicit unknown state before ready and reads quotas only after the GUI handshake', { timeout: 6000 }, async t => {
  const { host, reader, views, events, send, statePath } = await fixtureHost(t, { config: { autoReady: false }, pollIntervalMs: 20 });
  const first = await until(async () => (await views())[0], 'initial unknown view');
  assert.deepEqual(first.rows.map(row => [row.value, row.date, row.time, row.tone]), [['—', '—', '—', 'unknown'], ['—', '—', '—', 'unknown']]);
  assert(isAbsolute((await events())[0].args[0]));
  assert.equal((await events())[0].args[0], statePath);
  await delay(70);
  assert.equal(reader.calls, 0);
  await host.refresh();
  assert.equal(reader.calls, 0);
  await send('ready');
  const known = await until(async () => (await views()).find(view => view.rows[0].value === '38%'), 'normalized quota');
  assert.deepEqual(known.rows.map(row => [row.period, row.value, row.date, row.time, row.tone]), [['5 ч', '38%', '23.10', '19:55', 'normal'], ['Нед.', '25%', '29.10', '21:10', 'warning']]);
  assert.equal(known.collectedAt, instant);
  assert.equal(known.stale, false);
});

test('host coalesces overlapping timer and GUI refreshes into one provider read', { timeout: 6000 }, async t => {
  let finishRead;
  const pending = new Promise(resolve => { finishRead = resolve; });
  const reader = readerFor(call => call === 1 ? pending : quota);
  const { host, views, send } = await fixtureHost(t, { reader, pollIntervalMs: 20 });
  await until(() => reader.calls === 1, 'first provider call');
  const first = host.refresh();
  assert.equal(host.refresh(), first);
  await send('refresh');
  await send('refresh');
  await delay(80);
  assert.equal(reader.calls, 1);
  finishRead(quota);
  await first;
  await until(async () => (await views()).some(view => view.rows[0].value === '38%'), 'coalesced result');
});

test('startup mode waits for ChatGPT and suspends quota reads while it is closed', {timeout:6000}, async t=>{
  const {host,reader,send}=await fixtureHost(t,{waitForApp:true,pollIntervalMs:25});
  await delay(100);await host.refresh();assert.equal(reader.calls,0,'Windows logon without ChatGPT must not read quotas');
  await send('app-open');await until(()=>reader.calls>=1,'ChatGPT-ready read');
  await send('app-closed');await until(()=>host.appPresent===false,'closed app signal');
  const before=reader.calls;await send('refresh');await delay(100);await host.refresh();assert.equal(reader.calls,before,'background timer/manual refresh must wait for ChatGPT');
  await send('app-open');await until(()=>reader.calls>before,'reopened app refresh');
});

test('startup mode releases its actual quota backend while closed and reconnects on reopening', {timeout:6000}, async t=>{
  const reader=new RateLimitReader({command:process.execPath,args:[appServerFixture],timeoutMs:1000});
  const {send}=await fixtureHost(t,{reader,waitForApp:true});
  await delay(80);assert.equal(reader.child,undefined,'logon must not spawn a quota backend before ChatGPT');
  await send('app-open');await until(()=>reader.child?.pid,'first backend');const first=reader.child;
  await until(()=>reader.pending.size===0&&reader.reading===null,'initial quota completion');
  await send('app-closed');await until(()=>first.exitCode!==null||first.signalCode!==null,'backend exit on client close');assert.equal(reader.child,null);
  await send('app-open');await until(()=>reader.child?.pid&&reader.child.pid!==first.pid,'new backend after reopening');
});

test('taskbar mode enabled before ready starts quota polling without an open ChatGPT window', { timeout: 6000 }, async t => {
  const { host, reader, events, views, send } = await fixtureHost(t, { waitForApp: true, config: { autoReady: false }, pollIntervalMs: 25 });
  await send('taskbar-on');
  await until(async () => (await events()).some(event => event.command === 'taskbar-on'), 'taskbar mode command');
  await delay(60);
  await host.refresh();
  assert.equal(reader.calls, 0, 'taskbar mode still waits for the GUI handshake');
  assert((await views()).every(view => view.rows.every(row => row.value === '—')));
  await send('ready');
  await until(() => reader.calls >= 1, 'taskbar quota read before ChatGPT opens');
  assert.equal(host.taskbarEnabled, true);
  assert.equal(host.appPresent, false);
  await until(async () => (await views()).some(view => view.rows[0].value === '38%'), 'actual taskbar quota result');
  const before = reader.calls;
  await until(() => reader.calls > before, 'continued taskbar polling while ChatGPT is closed');
});

test('taskbar mode keeps the actual backend alive across ChatGPT close and releases it when both views are disabled', { timeout: 6000 }, async t => {
  const reader = new RateLimitReader({ command: process.execPath, args: [appServerFixture], timeoutMs: 1000 });
  const { host, send } = await fixtureHost(t, { reader, waitForApp: true });
  await delay(60);
  assert.equal(reader.child, undefined);
  assert.equal(host.taskbarEnabled, false);
  await send('taskbar-on');
  await until(() => reader.child?.pid, 'backend for taskbar mode');
  const first = reader.child;
  await until(() => reader.pending.size === 0 && reader.reading === null, 'taskbar quota completion');
  await send('app-open');
  await until(() => host.appPresent, 'ChatGPT open signal');
  await send('app-closed');
  await until(() => !host.appPresent, 'ChatGPT closed with taskbar mode enabled');
  assert.equal(reader.child, first);
  assert.equal(first.killed, false);
  await host.refresh();
  assert.equal(reader.child, first, 'closing ChatGPT must not restart the taskbar backend');
  await send('taskbar-off');
  await until(() => first.exitCode !== null || first.signalCode !== null, 'backend exit after taskbar mode disabled');
  assert.equal(host.taskbarEnabled, false);
  assert.equal(reader.child, null);
  await host.refresh();
  assert.equal(reader.child, null, 'disabled taskbar mode must not restart the closed backend');
  await send('app-open');
  await until(() => reader.child?.pid && reader.child.pid !== first.pid, 'backend after ChatGPT reopening');
  const reopened = reader.child;
  await send('taskbar-on');
  await until(() => host.taskbarEnabled, 'taskbar enabled with ChatGPT open');
  await send('taskbar-off');
  await until(() => !host.taskbarEnabled, 'taskbar disabled with ChatGPT open');
  assert.equal(reader.child, reopened);
  assert.equal(reopened.killed, false, 'ChatGPT still needs its quota backend');
  await send('app-closed');
  await until(() => reopened.exitCode !== null || reopened.signalCode !== null, 'normal backend exit with taskbar mode disabled');
});

test('taskbar mode respects pause and coalesces repeated enable and refresh requests', { timeout: 6000 }, async t => {
  let finishRead;
  const pending = new Promise(resolve => { finishRead = resolve; });
  const reader = readerFor(call => call === 1 ? pending : quota);
  const { host, events, send } = await fixtureHost(t, { reader, waitForApp: true, pollIntervalMs: 25 });
  await send('pause');
  await until(() => host.paused, 'pause before taskbar activation');
  await send('taskbar-on');
  await until(async () => (await events()).some(event => event.command === 'taskbar-on'), 'paused taskbar activation');
  await delay(60);
  assert.equal(reader.calls, 0);
  await send('resume');
  await until(() => reader.calls === 1, 'taskbar resume read');
  const refresh = host.refresh();
  assert.equal(host.refresh(), refresh);
  await send('taskbar-on');
  await send('taskbar-on');
  await send('refresh');
  await delay(80);
  assert.equal(reader.calls, 1, 'the timer and duplicate commands share one provider request');
  await send('pause');
  await until(() => host.paused, 'pause during taskbar read');
  finishRead(quota);
  await refresh;
  const calls = reader.calls;
  await send('taskbar-off');
  await until(() => !host.taskbarEnabled, 'disable paused taskbar mode');
  await send('taskbar-on');
  await until(() => host.taskbarEnabled, 're-enable paused taskbar mode');
  await delay(60);
  assert.equal(reader.calls, calls, 're-enabling taskbar mode must preserve pause');
  await send('resume');
  await until(() => reader.calls > calls, 'resume after taskbar mode re-enabled');
});

test('taskbar mode preserves unknown quotas and ages real values while a provider stalls', { timeout: 6000 }, async t => {
  let clock = instant;
  let finishUnknown;
  const unknownResult = new Promise(resolve => { finishUnknown = resolve; });
  let finishRead;
  const pending = new Promise(resolve => { finishRead = resolve; });
  const reader = readerFor(call => call === 1 ? unknownResult : call === 2 ? quota : pending);
  const { host, views, send } = await fixtureHost(t, { reader, waitForApp: true, now: () => clock });
  await send('taskbar-on');
  await until(() => reader.calls === 1, 'unknown taskbar quota request');
  const unknownRefresh = host.refresh();
  clock += 1000;
  finishUnknown(null);
  await unknownRefresh;
  const unknown = await until(async () => (await views()).find(view => view.collectedAt === instant + 1000), 'unknown taskbar quota response');
  assert.deepEqual(unknown.rows.map(row => [row.value, row.remaining, row.tone]), [['—', null, 'unknown'], ['—', null, 'unknown']]);
  await host.refresh();
  const known = await until(async () => (await views()).find(view => view.rows[0].value === '38%'), 'real taskbar quota response');
  assert.equal(known.collectedAt, instant + 1000);
  const refresh = host.refresh();
  await until(() => reader.calls === 3, 'stalled taskbar provider');
  clock += 181000;
  const stale = await until(async () => (await views()).find(view => view.stale && view.rows[0].value === '38%'), 'taskbar freshness clock');
  assert.deepEqual(stale.rows.map(row => [row.value, row.marker]), [['38%', 'устар.'], ['25%', 'устар.']]);
  await host.stop();
  const count = (await views()).length;
  finishRead(quota);
  await refresh;
  assert.equal((await views()).length, count);
});

test('stopping taskbar mode cancels update checks and prevents queued reads or later enable commands from restarting it', { timeout: 6000 }, async t => {
  let finishUpdate;
  let cancelled = 0;
  const updateChecker = { check: () => new Promise(resolve => { finishUpdate = resolve; }), cancel: () => { cancelled++; } };
  const { host, reader, views, send } = await fixtureHost(t, { waitForApp: true, updateChecker, config: { ignoreEof: true } });
  await send('taskbar-on');
  await until(() => reader.calls === 1, 'initial taskbar read');
  await send('check-updates');
  await until(() => finishUpdate, 'separate update check');
  const refresh = host.refresh();
  const stopped = host.stop();
  await send('taskbar-on');
  await send('resume');
  await refresh;
  await stopped;
  const count = (await views()).length;
  finishUpdate({ state: 'current' });
  await host.refresh();
  await host.stop();
  await delay(30);
  assert.equal(reader.calls, 1);
  assert.equal(reader.closes, 1);
  assert.equal(cancelled, 1);
  assert.equal((await views()).length, count);
  assert(!(await views()).some(view => view.update?.state === 'current'));
});

test('pause stops reads and resume refreshes immediately without starting another polling loop', { timeout: 6000 }, async t => {
  const { host, reader, events, send } = await fixtureHost(t, { pollIntervalMs: 25 });
  await until(() => reader.calls >= 1, 'initial read');
  await send('pause');
  await until(() => host.paused === true, 'pause command');
  const pausedCalls = reader.calls;
  await host.refresh();
  await send('refresh');
  await delay(90);
  assert.equal(reader.calls, pausedCalls);
  await send('resume');
  await until(() => reader.calls > pausedCalls, 'resume read');
  assert.equal(host.paused, false);
  await send('pause');
  await until(() => host.paused === true, 'second pause');
  assert((await events()).some(event => event.command === 'resume'));
});

test('failed reads retain the last known values, mark them stale, and recover on the next success', { timeout: 6000 }, async t => {
  const reader = readerFor(call => { if (call === 2) throw new Error('private backend diagnostic'); return quota; });
  const { host, views } = await fixtureHost(t, { reader });
  await until(async () => (await views()).some(view => view.rows[0].value === '38%'), 'first success');
  await host.refresh();
  const stale = await until(async () => (await views()).find(view => view.stale && view.rows[0].value === '38%'), 'stale retained values');
  assert.equal(stale.collectedAt, instant);
  assert(stale.rows.every(row => row.marker === 'устар.'));
  assert(!JSON.stringify(await views()).includes('private backend diagnostic'));
  await host.refresh();
  await until(async () => { const last = (await views()).at(-1); return last?.rows[0].value === '38%' && !last.stale; }, 'successful recovery');
});

test('freshness changes while a provider read hangs and shutdown ignores its late result', { timeout: 6000 }, async t => {
  let clock = instant;
  let finishRead;
  const pending = new Promise(resolve => { finishRead = resolve; });
  const reader = readerFor(call => call === 1 ? quota : pending);
  const { host, views } = await fixtureHost(t, { reader, now: () => clock });
  await until(async () => (await views()).some(view => view.rows[0].value === '38%'), 'known values');
  const refresh = host.refresh();
  await until(() => reader.calls === 2, 'hanging provider call');
  clock += 181000;
  const stale = await until(async () => (await views()).find(view => view.stale && view.rows[0].value === '38%'), 'independent freshness update');
  assert(stale.rows.every(row => row.marker === 'устар.'));
  await host.stop();
  const before = (await views()).length;
  finishRead(quota);
  await refresh;
  await delay(30);
  assert.equal((await views()).length, before);
  assert.equal(reader.closes, 1);
});

test('a queued refresh cannot restart the provider after shutdown closes it', { timeout: 6000 }, async t => {
  const { host, reader, views } = await fixtureHost(t);
  await until(async () => (await views()).some(view => view.rows[0].value === '38%'), 'known values');
  const calls = reader.calls;
  const refresh = host.refresh();
  const stopped = host.stop();
  await refresh;
  await stopped;
  assert.equal(reader.calls, calls);
  assert.equal(reader.closes, 1);
});

test('GUI exit command closes its input, stops polling, and closes the quota reader exactly once', { timeout: 6000 }, async t => {
  const { host, reader, events, send } = await fixtureHost(t, { pollIntervalMs: 20 });
  await until(() => reader.calls >= 1, 'initial read');
  await send('exit');
  const result = await host.done;
  assert.equal(result.code, 0);
  assert.equal(reader.closes, 1);
  assert((await events()).some(event => event.event === 'eof'));
  const calls = reader.calls;
  await delay(60);
  await host.stop();
  assert.equal(reader.calls, calls);
  assert.equal(reader.closes, 1);
});

test('a crashed GUI terminates the host and reader with a failure result', { timeout: 6000 }, async t => {
  const { host, reader, send } = await fixtureHost(t);
  await send('crash');
  const result = await host.done;
  assert.equal(result.code, 1);
  assert.equal(reader.closes, 1);
});

test('EPIPE is contained and a GUI that ignores EOF is killed after bounded shutdown', { timeout: 6000 }, async t => {
  const { host, reader } = await fixtureHost(t, { config: { ignoreEof: true } });
  await until(() => reader.calls >= 1, 'initial read');
  // Destroying Node's own stdin does not close Windows' inherited pipe handle.
  // Inject the OS error at the actual host Writable boundary instead.
  host.child.stdin.destroy(Object.assign(new Error('write EPIPE'), { code: 'EPIPE' }));
  const result = await host.done;
  assert.equal(result.code, 1);
  assert.equal(result.reason, 'gui-input-error');
  assert.equal(reader.closes, 1);
});

test('a GUI input stream that closes without an error also terminates its host', { timeout: 6000 }, async t => {
  const { host, reader } = await fixtureHost(t, { config: { ignoreEof: true } });
  await until(() => reader.calls >= 1, 'initial read');
  host.child.stdin.destroy();
  const result = await host.done;
  assert.equal(result.code, 1);
  assert.equal(reader.closes, 1);
});

test('spawn failures resolve cleanly and create the requested state directory', { timeout: 6000 }, async t => {
  assert.equal(typeof api.createOverlayHost, 'function');
  const directory = await mkdtemp(join(tmpdir(), 'quota overlay missing '));
  const statePath = join(directory, 'new directory', 'state.json');
  const reader = readerFor();
  let options;
  const host = api.createOverlayHost({ reader, guiCommand: join(directory, 'missing.exe'), statePath, spawnGui: (command, args, settings) => { options = { command, args, settings }; return spawn(command, args, settings); } });
  t.after(async () => { await host.stop(); await rm(directory, { recursive: true, force: true }); });
  const result = await host.done;
  assert.equal(result.code, 1);
  assert.equal(reader.calls, 0);
  assert.equal(reader.closes, 1);
  assert.equal(options.settings.windowsHide, true);
  assert.deepEqual(options.args, [statePath]);
  await writeFile(statePath, '{}');
});

test('host reuses the read-only App Server reader and shuts down its actual child process', { timeout: 6000 }, async t => {
  const reader = new RateLimitReader({ command: process.execPath, args: [appServerFixture], timeoutMs: 1000 });
  const { host, views } = await fixtureHost(t, { reader });
  const known = await until(async () => (await views()).find(view => view.rows[0].value === '38%'), 'App Server response');
  assert.equal(known.rows[1].value, '—');
  const backend = reader.child;
  assert(backend?.pid);
  await host.stop();
  await until(() => backend.exitCode !== null || backend.signalCode !== null, 'backend subprocess exit');
});

test('manual update command uses a separate coalesced checker without changing quota and stops replying after shutdown',async t=>{
  let count=0,finish;const updateChecker={check:()=>{count++;return new Promise(resolve=>{finish=resolve;});}};
  const {host,views,send,reader}=await fixtureHost(t,{updateChecker,config:{autoReady:false}});
  assert.equal(count,0);await send('check-updates');await until(()=>count===1);
  await send('check-updates');await delay(30);assert.equal(count,1);assert.equal(reader.calls,0);
  assert((await views()).some(view=>view.update?.state==='checking'));
  finish({state:'available',currentVersion:'1.1.0',latestVersion:'1.2.0',url:'https://github.com/MrBogomoll/chatgpt-limit-panel/releases/tag/v1.2.0'});
  await until(async()=> (await views()).some(view=>view.update?.state==='available'));
  assert.equal(reader.calls,0);await send('check-updates');await until(()=>count===2);await host.stop();finish({state:'current'});await delay(20);
  assert(!(await views()).some(view=>view.update?.state==='current'));
});


test('live tray keeps quota refreshing independently of ChatGPT and taskbar and respects pause', {timeout:6000}, async t=>{
  const {host,reader,send,views}=await fixtureHost(t,{waitForApp:true,pollIntervalMs:25});
  await delay(60);assert.equal(reader.calls,0);
  await send('tray-live-on');await until(()=>reader.calls>0,'live tray data without open app');
  await until(async()=>(await views()).some(v=>v.rows[0].value==='38%'),'actual live tray values');
  await send('taskbar-on');await delay(40);await send('taskbar-off');
  await delay(40);const before=reader.calls;await until(()=>reader.calls>before,'live tray survives disabling taskbar');
  await send('pause');await until(()=>host.paused,'pause');const pausedCalls=reader.calls;await delay(80);assert.equal(reader.calls,pausedCalls);
  await send('resume');await until(()=>reader.calls>pausedCalls,'resume live tray');
  await send('tray-live-off');await delay(50);const stopped=reader.calls;await host.refresh();assert.equal(reader.calls,stopped);
  await send('app-open');await until(()=>reader.calls>stopped,'normal app polling');
  await send('tray-live-on');await send('app-closed');await delay(50);const closed=reader.calls;await until(()=>reader.calls>closed,'live polling after app closes');
  await send('tray-live-off');await delay(50);const disabled=reader.calls;await delay(60);assert.equal(reader.calls,disabled);
});
