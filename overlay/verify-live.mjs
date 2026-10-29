import assert from 'node:assert/strict';
import { readFileSync, statSync } from 'node:fs';
import { homedir } from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

function processIsAlive(pid) {
  process.kill(pid, 0); // Signal zero checks existence without stopping or changing the process.
  return true;
}

export function verifyOverlayState(state, { isProcessAlive = processIsAlive, updatedAt, now = Date.now() } = {}) {
  assert(state && typeof state === 'object', 'overlay state must be an object');
  assert(Number.isSafeInteger(state.process) && state.process > 0, 'overlay process must report a positive PID');
  let alive = false;
  try { alive = isProcessAlive(state.process) === true; }
  catch (error) {
    if (error.code !== 'ESRCH') throw new Error(`overlay process liveness could not be checked: ${error.message}`);
  }
  assert(alive, `overlay process ${state.process} is not running`);
  assert(Number.isFinite(updatedAt) && Number.isFinite(now) && now - updatedAt <= 10_000,
    'overlay state is stale: the state file must have been updated within ten seconds');
  assert(state.owner===0||state.ownerProcess===state.process,'overlay must not attach UI input queues through a cross-process owner');
  assert(state.owner===0||state.owner!==state.target,'ChatGPT must not own the overlay window');
  assert.equal(state.noActivate,true,'overlay must not activate itself');
  assert.equal(state.clickThrough,false,'panel must receive its own clicks instead of forwarding them to ChatGPT');
  assert.equal(state.clickToToggle,true,'panel must allow a click to toggle compact mode');
  assert.equal(typeof state.compact,'boolean','effective presentation must be reported');
  assert.equal(typeof state.preferredCompact,'boolean','saved preference must be reported');
  assert.equal(typeof state.visible,'boolean','overlay visibility must be reported');
  const mode=state.mode??(state.compact?'compact':'full');
  assert(['full','compact','ultra'].includes(mode),'effective mode must be full, compact or ultra');
  assert.equal(state.compact,mode!=='full','effective mode must match legacy presentation flag');
  if(state.preferredMode!==undefined) {
    assert(['full','compact','ultra'].includes(state.preferredMode),'saved mode must be valid');
    assert.equal(state.preferredCompact,state.preferredMode!=='full','saved mode must match legacy preference flag');
  }
  if(state.visible) {
    assert(Number.isFinite(state.dpi) && state.dpi > 0, 'visible overlay must report a positive DPI');
    for (const [name, rectangle] of [['client', state.client], ['panel', state.panel]]) {
      assert(rectangle && ['x', 'y', 'width', 'height'].every(key => Number.isFinite(rectangle[key]))
        && rectangle.width > 0 && rectangle.height > 0, `${name} bounds must be finite with positive dimensions`);
    }
    assert.equal(state.targetVisible,true,'only show on a visible, restored target');
    assert.equal(typeof state.topmost,'boolean','panel must report its topmost band');
    assert.equal(typeof state.targetTopmost,'boolean','target must report its topmost band');
    assert.equal(state.topmost,state.targetTopmost,'panel must follow the target topmost band');
    assert(state.panel.y>=state.client.y&&state.panel.x+state.panel.width<=state.client.x+state.client.width,
      'panel must stay within client bounds');
    assert(state.panel.x>=state.client.x&&state.panel.x+state.panel.width<=state.client.x+52*state.dpi/96,'stay within rail');
    assert(state.panel.y+state.panel.height<=state.client.y+state.client.height-110*state.dpi/96,'leave update button and avatar free');
    assert(Math.abs(state.panel.height-({full:192,compact:100,ultra:32}[mode])*state.dpi/96)<1,'effective mode must use the expected scaled height');
  }
  assert.equal(state.view.rows.length,2);
  return {result:'PASS',visible:state.visible,compact:state.compact,preferredCompact:state.preferredCompact,dpi:state.dpi,owner:state.owner,rows:state.view.rows};
}

function run() {
  try {
    const localData = process.env.LOCALAPPDATA || path.join(homedir(), 'AppData', 'Local');
    const stateFile = path.join(localData, 'ChatGPTLimitPanel', 'state.json');
    const state = JSON.parse(readFileSync(stateFile, 'utf8').replace(/^\uFEFF/, ''));
    const result = verifyOverlayState(state, { updatedAt: statSync(stateFile).mtimeMs });
    process.stdout.write(JSON.stringify(result) + '\n');
  } catch (error) {
    process.stderr.write(`Overlay diagnostics failed: ${error.message}\n`);
    process.exitCode = 1;
  }
}

if (process.argv[1] && pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url) run();
