import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtemp, mkdir, writeFile, utimes, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { verifyOverlayState } from '../overlay/verify-live.mjs';

const checker = fileURLToPath(new URL('../overlay/verify-live.mjs', import.meta.url));

function healthyState() {
  return {
    visible: true, paused: false, compact: true, preferredCompact: true,
    process: process.pid, owner: 0, ownerProcess: 0, target: 100, foreground: 100,
    dpi: 96, client: { x: 100, y: 200, width: 1000, height: 800 },
    panel: { x: 103, y: 790, width: 46, height: 100 },
    noActivate: true, clickThrough: false, clickToToggle: true,
    view: { rows: [{ period: '5 ч', value: '28%' }, { period: 'Нед.', value: '89%' }] },
  };
}

test('diagnostics accepts the explicit ultracompact geometry and rejects invalid or inconsistent mode reports', () => {
  const state={...healthyState(),mode:'ultra',preferredMode:'ultra'};
  state.panel={...state.panel,y:858,height:32};
  assert.equal(verifyOverlayState(state,{updatedAt:Date.now()}).result,'PASS');
  assert.throws(()=>verifyOverlayState({...state,mode:'invalid'},{updatedAt:Date.now()}),/mode/i);
  assert.throws(()=>verifyOverlayState({...state,compact:false},{updatedAt:Date.now()}),/mode/i);
  assert.throws(()=>verifyOverlayState({...state,panel:{...state.panel,y:790,height:100}},{updatedAt:Date.now()}),/height/i);
});

async function runChecker(t, state, ageMs = 0) {
  const directory = await mkdtemp(path.join(tmpdir(), 'quota diagnostics тест '));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const stateDirectory = path.join(directory, 'ChatGPTQuotaOverlay');
  await mkdir(stateDirectory);
  const stateFile = path.join(stateDirectory, 'state.json');
  await writeFile(stateFile, '\uFEFF' + JSON.stringify(state));
  if (ageMs) {
    const timestamp = new Date(Date.now() - ageMs);
    await utimes(stateFile, timestamp, timestamp);
  }
  return spawnSync(process.execPath, [checker], {
    env: { ...process.env, LOCALAPPDATA: directory },
    encoding: 'utf8', windowsHide: true, timeout: 5000,
  });
}

test('diagnostics CLI accepts a fresh state for this live Node process on every platform', async t => {
  const result = await runChecker(t, healthyState());
  assert.ifError(result.error);
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stderr, '');
  assert.deepEqual(JSON.parse(result.stdout), {
    result: 'PASS', visible: true, compact: true, preferredCompact: true,
    dpi: 96, owner: 0, rows: healthyState().view.rows,
  });
});

test('diagnostics CLI rejects a fresh saved state after its own reported process has exited', async t => {
  const exited = spawnSync(process.execPath, ['-e', 'process.stdout.write(String(process.pid))'], {
    encoding: 'utf8', windowsHide: true, timeout: 5000,
  });
  assert.ifError(exited.error);
  assert.equal(exited.status, 0, exited.stderr);
  const state = { ...healthyState(), process: Number(exited.stdout) };
  const result = await runChecker(t, state);
  assert.ifError(result.error);
  assert.notEqual(result.status, 0, 'a stale file must not prove a stopped process is live');
  assert.equal(result.stdout, '');
  assert.match(result.stderr, /process.*(not running|not alive|stopped)/i);
});

test('diagnostics CLI rejects state files older than ten seconds even while their process is alive', async t => {
  const result = await runChecker(t, healthyState(), 10001);
  assert.ifError(result.error);
  assert.notEqual(result.status, 0, 'old diagnostic state must not produce PASS');
  assert.equal(result.stdout, '');
  assert.match(result.stderr, /state.*(stale|old|recent|updated)/i);
});

test('diagnostics CLI keeps rejecting a panel outside the left navigation rail', async t => {
  const state = healthyState();
  state.panel.x = 200;
  const result = await runChecker(t, state);
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /rail/i);
});

test('diagnostics CLI keeps rejecting a panel that can activate itself', async t => {
  const result = await runChecker(t, { ...healthyState(), noActivate: false });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /activate/i);
});

const instant = 1792866000000;
const fresh = { updatedAt: instant, now: instant };

test('diagnostics accepts the ten-second freshness boundary and rejects one millisecond beyond it', () => {
  assert.equal(verifyOverlayState(healthyState(), { ...fresh, updatedAt: instant - 10000 }).result, 'PASS');
  assert.throws(() => verifyOverlayState(healthyState(), { ...fresh, updatedAt: instant - 10001 }), /state.*stale/i);
});

test('diagnostics rejects a missing PID when the operating system returns ESRCH', () => {
  assert.throws(() => verifyOverlayState(healthyState(), {
    ...fresh, isProcessAlive() { throw Object.assign(new Error('no such process'), { code: 'ESRCH' }); },
  }), /process.*not running/i);
});

test('diagnostics fails closed when process liveness cannot be established', () => {
  assert.throws(() => verifyOverlayState(healthyState(), {
    ...fresh, isProcessAlive() { throw Object.assign(new Error('access denied'), { code: 'EPERM' }); },
  }), /liveness could not be checked/i);
  assert.throws(() => verifyOverlayState(healthyState(), { ...fresh, isProcessAlive: () => false }), /not running/i);
});

test('diagnostics requires a positive PID and a real update timestamp', () => {
  for (const pid of [undefined, 0, -1, 1.5, '123']) {
    assert.throws(() => verifyOverlayState({ ...healthyState(), process: pid }, fresh), /positive PID/i);
  }
  assert.throws(() => verifyOverlayState(healthyState(), { now: instant }), /state.*stale/i);
});

test('diagnostics accepts a live hidden panel while its target is unavailable', () => {
  const state = { ...healthyState(), visible: false, target: 0, foreground: 999,
    client: { x: 0, y: 0, width: 0, height: 0 }, panel: { x: 3, y: -210, width: 46, height: 100 } };
  assert.equal(verifyOverlayState(state, fresh).visible, false);
});

for (const [description, change, message] of [
  ['cross-process ownership', state => { state.owner = 555; state.ownerProcess = 999; }, /cross-process owner/i],
  ['ownership by the ChatGPT target', state => { state.owner = state.target; state.ownerProcess = state.process; }, /ChatGPT must not own/i],
  ['click-through input', state => { state.clickThrough = true; }, /own clicks/i],
  ['disabled click toggling', state => { state.clickToToggle = false; }, /toggle compact/i],
  ['missing effective mode', state => { delete state.compact; }, /effective presentation/i],
  ['missing preferred mode', state => { delete state.preferredCompact; }, /saved preference/i],
  ['a background target', state => { state.foreground = 999; }, /active ChatGPT/i],
  ['overlap of the avatar clearance', state => { state.panel.y = 791; }, /avatar free/i],
  ['wrong panel height', state => { state.panel.height = 99; }, /scaled height/i],
  ['a missing quota row', state => { state.view.rows.pop(); }, /2/],
]) {
  test(`diagnostics preserves rejection of ${description}`, () => {
    const state = healthyState();
    change(state);
    assert.throws(() => verifyOverlayState(state, fresh), message);
  });
}

for (const [description, change] of [
  ['negative panel width', state => { state.panel.width = -46; }],
  ['a non-finite coordinate', state => { state.panel.y = -Infinity; }],
  ['a panel above its client area', state => { state.panel.y = 0; }],
  ['a negative client width', state => { state.client.width = -1000; }],
  ['a non-boolean visibility flag', state => { state.visible = 'false'; }],
]) {
  test(`diagnostics rejects corrupted bounds: ${description}`, () => {
    const state = healthyState();
    change(state);
    assert.throws(() => verifyOverlayState(state, fresh));
  });
}
