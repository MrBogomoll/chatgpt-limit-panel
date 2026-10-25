// Explicit live acceptance for a relocated beta package. Never run as part of unit tests.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { createInterface } from 'node:readline';
import path from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { verifyOverlayState } from './verify-live.mjs';
const root=path.resolve(process.argv[2]||'');
assert(process.argv[2]&&process.platform==='win32','pass the relocated Windows beta package directory');
const sessions=[];
function connect() {
  const child=spawn(path.join(root,'runtime/node.exe'),[path.join(root,'server/index.mjs')],{cwd:path.dirname(root),windowsHide:true,stdio:['pipe','pipe','pipe']});
  const pending=new Map();let id=0;
  const lines=createInterface({input:child.stdout});child.stderr.resume();
  lines.on('line',line=>{const response=JSON.parse(line);const entry=pending.get(response.id);if(entry){pending.delete(response.id);clearTimeout(entry.timer);entry.resolve(response);}});
  const closed=new Promise(resolve=>child.on('close',resolve));
  child.on('error',error=>{for(const entry of pending.values())entry.reject(error);});
  const session={
    request(method,params={}) {return new Promise((resolve,reject)=>{const requestId=++id;const timer=setTimeout(()=>{pending.delete(requestId);reject(new Error('relocated MCP request timed out'));},12000);pending.set(requestId,{resolve,reject,timer});child.stdin.write(JSON.stringify({jsonrpc:'2.0',id:requestId,method,params})+'\n');});},
    async close(){child.stdin.end();const result=await Promise.race([closed,delay(3000,'timeout')]);if(result==='timeout'){child.kill();throw new Error('MCP transport failed to close');}assert.equal(result,0);},
  };sessions.push(session);return session;
}
async function control(session,name) {
  const startedAt=Date.now();
  const response=await session.request('tools/call',{name,arguments:{}});
  assert(!response.error&&!response.result?.isError,`native tool ${name} must succeed`);
  console.log(JSON.stringify({operation:name,elapsedMs:Date.now()-startedAt}));
  return response.result.structuredContent;
}
let needsRecovery=false;
try {
  const session=connect();
  assert((await session.request('initialize',{protocolVersion:'2025-06-18'})).result.serverInfo.name==='quota-panel');
  const discovery=await session.request('tools/list');assert.equal(discovery.result.tools.length,4);
  const before=await control(session,'get_quota_overlay_status');
  const started=await control(session,'start_quota_overlay');assert(started.running);
  assert.equal(started.autoStartConfigured,true,'first native activation must configure the user logon task');
  assert.equal(typeof started.autoStartEnabled,'boolean','native status must expose the saved autostart choice');
  if(before.running)assert.equal(started.changed,false);
  const stopped=await control(session,'stop_quota_overlay');needsRecovery=true;
  assert(!stopped.running&&stopped.changed);
  const stoppedAgain=await control(session,'stop_quota_overlay');assert(!stoppedAgain.running&&!stoppedAgain.changed);
  const restartAt=Date.now();
  const restarted=await control(session,'start_quota_overlay');assert(restarted.running&&restarted.changed);needsRecovery=false;
  assert(Date.now()-restartAt<7000,'native start response must not wait for the controller eight-second timeout');
  const resource=await session.request('resources/read',{uri:'ui://quota-panel/control-v1.html'});
  assert(resource.result.contents[0].text.includes('Запустить'));
  const stateFile=path.join(process.env.LOCALAPPDATA,'ChatGPTQuotaOverlay/state.json');
  let state,diagnosticSource='state.json',updatedAt;
  const deadline=Date.now()+20000;
  do {
    state=JSON.parse((await readFile(stateFile,'utf8')).replace(/^\uFEFF/,''));
    // Some execution contexts retain an earlier copy of pre-existing files.
    // A fresh snapshot exported by our own GUI can independently prove startup.
    // It must belong to this start; an old startup snapshot never proves liveness.
    if(!(state.view?.collectedAt>=restartAt-1000)) {
      try {
        const diagnostic=JSON.parse((await readFile(path.join(path.dirname(stateFile),'startup.json'),'utf8')).replace(/^\uFEFF/,''));
        if(diagnostic.receivedUpdates>=2&&Date.parse(diagnostic.time)>=restartAt-1000&&diagnostic.monitorState?.process===diagnostic.process) {
          state=diagnostic.monitorState;updatedAt=Date.parse(diagnostic.time);diagnosticSource='own-GUI startup.json';
        }
      }catch{}
    }
    if(state.view?.rows?.every(row=>row.value!=='—')&&state.view.collectedAt>=restartAt-1000&&Date.now()-state.view.collectedAt<35000)break;
    await delay(100);
  }while(Date.now()<deadline);
  assert(state.view.rows.every(row=>row.value!=='—'),'relocated host must receive real quota data');
  assert(state.view.collectedAt>=restartAt-1000&&Date.now()-state.view.collectedAt<35000,'real quota snapshot must come from this startup and fit the thirty-second refresh cycle');
  process.kill(state.process,0);
  assert.equal(state.hoverTooltipEnabled,false,'installed panel must have no hover popup');
  assert.equal(typeof state.progress?.fullEnabled,'boolean','installed binary must include full progress settings');
  assert(['bars','outline','both','off'].includes(state.progress.fullMode),'full mode must support individual, combined and disabled indicators');
  assert.equal(state.progress.indicatorMode,state.progress.fullMode,'installed panel must expose the shared indicator choice');
  assert(['short','weekly'].includes(state.progress.fullWindow),'full contour must keep its own quota choice');
  assert.equal(typeof state.progress?.compactEnabled,'boolean','installed binary must include compact progress settings');
  assert(['short','weekly'].includes(state.progress.compactWindow),'installed outline source must be a valid saved selection');
  assert.equal(typeof state.progress.ultraEnabled,'boolean','installed panel must include ultracompact contour settings');
  const contourEnabled=['outline','both'].includes(state.progress.indicatorMode);
  assert.equal(state.progress.compactEnabled,contourEnabled,'compact contour must follow the shared indicator choice');
  assert.equal(state.progress.ultraEnabled,contourEnabled,'ultra contour must follow the shared indicator choice');
  assert.equal(state.progress.ultraWindow,state.progress.fullWindow,'all modes must use the same contour source');
  assert.equal(state.progress.compactWindow,state.progress.fullWindow,'compact must retain the shared contour source');
  assert(['full','compact','ultra'].includes(state.mode),'installed panel must report one of three presentation modes');
  assert.equal(state.startupDelayMs,3000,'installed GUI must use the three-second app readiness gate');
  assert.equal(state.appReady,true,'real quota collection must follow app readiness');
  assert(['auto','dark','light'].includes(state.theme?.mode),'installed panel must include saved theme selection');
  assert(['dark','light'].includes(state.theme.effective),'installed panel must resolve an effective theme');
  if(state.theme.mode!=='auto')assert.equal(state.theme.effective,state.theme.mode,'manual theme must override app/OS');
  if(state.theme.mode==='auto'&&['dark','light'].includes(state.theme.appPreference))assert.equal(state.theme.effective,state.theme.appPreference,'explicit app theme must override Windows');
  assert.deepEqual([state.progress.shortRemaining,state.progress.weeklyRemaining],state.view.rows.map(row=>row.remaining),'real remaining values must feed the progress indicators');
  if(updatedAt)verifyOverlayState(state,{updatedAt});
  await session.close();sessions.splice(sessions.indexOf(session),1);
  const reopened=connect();
  const afterClose=await control(reopened,'get_quota_overlay_status');assert(afterClose.running,'closing the plugin transport must leave the monitor running');
  await reopened.close();sessions.splice(sessions.indexOf(reopened),1);
  console.log(JSON.stringify({result:'PASS',packageRoot:root,tools:4,nativeStartStop:true,idempotent:true,realQuota:true,survivesTransportClose:true,noHoverTooltip:true,autostart:{configured:started.autoStartConfigured,enabled:started.autoStartEnabled,delayMs:state.startupDelayMs,appReady:state.appReady},progressSettings:state.progress,theme:state.theme,diagnosticSource,panelProcess:state.process,visible:state.visible,compact:state.compact,mode:state.mode}));
} finally {
  if(needsRecovery){const recovery=connect();try{await control(recovery,'start_quota_overlay');}catch{}}
  for(const session of sessions)try{await session.close();}catch{}
}
