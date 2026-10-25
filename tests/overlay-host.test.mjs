import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, writeFile, appendFile, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';
import { RateLimitReader } from '../plugins/quota-chips/server/reader.mjs';

const api = await import('../overlay/host.mjs').catch(() => ({}));
const fixture = fileURLToPath(new URL('./fixtures/overlay-gui.cjs', import.meta.url));
const appServerFixture = fileURLToPath(new URL('./fixtures/quota-app-server.cjs', import.meta.url));
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
