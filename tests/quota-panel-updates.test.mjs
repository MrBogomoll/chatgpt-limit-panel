import {test} from 'node:test';
import assert from 'node:assert/strict';
const api=await import('../plugins/quota-panel/server/updates.mjs').catch(()=>({}));
const origin='https://github.com/MrBogomoll/chatgpt-limit-panel';
function release(version='1.2.0',slug='chatgpt-limit-panel') {const base='https://github.com/MrBogomoll/'+slug;return {id:99,url:'https://api.github.com/repos/MrBogomoll/'+slug+'/releases/99',tag_name:'v'+version,draft:false,prerelease:false,html_url:base+'/releases/tag/v'+version,assets:[{name:`quota-panel-${version}-windows-x64.zip`,state:'uploaded',size:123,browser_download_url:base+`/releases/download/v${version}/quota-panel-${version}-windows-x64.zip`}]};}
const reply=(value,status=200,headers={})=>new Response(JSON.stringify(value),{status,headers});
function checker(options={}) {assert.equal(typeof api.createUpdateChecker,'function');return api.createUpdateChecker({currentVersion:'1.1.0',cooldownMs:0,...options});}

test('the local beta reports its own version and does not offer the older stable release',async()=>{
  const value=await api.createUpdateChecker({fetchImpl:async()=>reply(release('1.2.0')),cooldownMs:0}).check();
  assert.equal(value.currentVersion,'1.2.1-beta.5');assert.equal(value.latestVersion,'1.2.0');
  assert.equal(value.state,'ahead');assert.equal(value.url,null);
});

test('stable numeric versions compare correctly without treating a development version as outdated',async()=>{
  assert.equal(typeof api.compareVersions,'function');
  for(const [a,b,result] of [['1.10.0','1.9.0',1],['1.1.0','1.1.0',0],['1.0.9','1.1.0',-1],['v2.0.0','1.99.99',1]])assert.equal(api.compareVersions(a,b),result);
  for(const bad of ['1.1','1.01.0','1.1.0+dev','x1.1.0','1.1.0/evil'])assert.throws(()=>api.compareVersions(bad,'1.1.0'));
  for(const [latest,state] of [['1.2.0','available'],['1.1.0','current'],['1.0.1','ahead']]) {
    const value=await checker({fetchImpl:async()=>reply(release(latest))}).check();
    assert.equal(value.state,state);assert.equal(value.currentVersion,'1.1.0');assert.equal(value.latestVersion,latest);
    assert.equal(value.url,state==='available'?origin+'/releases/tag/v'+latest:null);
  }
});
test('installed prereleases compare by numeric core and SemVer identifier precedence',()=>{
  const ordered=['1.2.0-alpha','1.2.0-alpha.1','1.2.0-alpha.beta','1.2.0-beta','1.2.0-beta.2','1.2.0-beta.11','1.2.0-rc.1','1.2.0'];
  for(let i=0;i<ordered.length;i++)for(let j=0;j<ordered.length;j++)assert.equal(api.compareVersions(ordered[i],ordered[j]),Math.sign(i-j),`${ordered[i]} versus ${ordered[j]}`);
  for(const [a,b,result] of [['v1.2.0-beta.1','1.2.0-beta.1',0],['1.2.0-beta.2','1.2.0-beta.10',-1],['1.2.0-beta.9007199254740993','1.2.0-beta.9007199254740992',1],['1.2.0-beta.1','1.1.99',1],['1.2.0-beta.1','1.2.1-alpha',-1],['1.2.0-1','1.2.0-alpha',-1],['1.2.0-Beta','1.2.0-beta',-1],['1.2.0-beta-1','1.2.0-beta-1',0],['1.3.3','1.2.0',1]])assert.equal(api.compareVersions(a,b),result);
  for(const bad of ['1.2.0-','1.2.0-beta.','1.2.0-.beta','1.2.0-beta..1','1.2.0-01','1.2.0-beta.01','1.2.0-beta_1','1.2.0-béta.1','1.2.0-beta.1+build','1.2.0-beta.1/evil','1.2.0-beta.1\n','1.2.0\n','1.2.0-beta.'+'x'.repeat(55),null,120])assert.throws(()=>api.compareVersions(bad,'1.2.0'));
});
test('a beta installation can find its stable release while the public feed remains stable-only',async()=>{
  for(const [current,latest,state] of [['1.2.0-beta.1','1.2.0','available'],['1.2.0-beta.11','1.1.0','ahead'],['1.2.0','1.2.0','current'],['1.3.3','1.2.0','ahead']]) {
    const value=await checker({currentVersion:current,fetchImpl:async()=>reply(release(latest))}).check();
    assert.equal(value.state,state);assert.equal(value.currentVersion,current);assert.equal(value.latestVersion,latest);assert.equal(value.url,state==='available'?origin+'/releases/tag/v'+latest:null);
  }
  for(const currentVersion of ['1.2.0-beta.01','1.2.0-beta.1+build','1.2.0-beta.1\n'])assert.throws(()=>checker({currentVersion}));
  for(const [tag,markedPrerelease] of [['1.2.0-beta.1',false],['1.2.0-rc.1',false],['1.2.0-beta.1',true],['1.2.0',true]]) {
    const data=release(tag);data.prerelease=markedPrerelease;
    const value=await checker({currentVersion:'1.2.0-beta.1',fetchImpl:async()=>reply(data)}).check();assert.equal(value.state,'error');assert.equal(value.reason,'invalid');assert.equal(value.url,null);
  }
});
test('checks are explicit, public, coalesced and briefly cached without lifecycle or authentication',async()=>{
  let calls=0,finish,clock=1000;
  const check=checker({cooldownMs:15000,now:()=>clock,fetchImpl:(url,options)=>{calls++;assert.equal(url,'https://api.github.com/repositories/1433486830/releases/latest');assert.equal(options.headers.Authorization,undefined);assert.equal(options.redirect,'error');return new Promise(resolve=>{finish=resolve;});}});
  assert.equal(calls,0);const first=check.check();const second=check.check();await Promise.resolve();assert.equal(calls,1);
  finish(reply(release()));assert.deepEqual(await first,await second);await check.check();assert.equal(calls,1);
  clock+=16000;const third=check.check();await Promise.resolve();assert.equal(calls,2);finish(reply(release('1.1.0')));assert.equal((await third).state,'current');
});
test('offline, rate limits, missing releases and malformed JSON remain errors rather than up to date',async()=>{
  for(const [fetchImpl,reason] of [[async()=>{throw new Error('private path token');},'network'],[async()=>reply({},403),'rate-limit'],[async()=>reply({},429),'rate-limit'],[async()=>reply({},404),'not-found'],[async()=>new Response('{'),'invalid'],[async()=>reply({},500),'network']]) {
    const value=await checker({fetchImpl}).check();assert.equal(value.state,'error');assert.equal(value.reason,reason);assert.equal(value.url,null);assert.doesNotMatch(JSON.stringify(value),/private path token/);
  }
});
test('timeout is bounded even when a provider never settles and aborts its request',async()=>{
  let signal;const value=await checker({timeoutMs:25,fetchImpl:async(url,options)=>{signal=options.signal;return new Promise(()=>{});}}).check();
  assert.equal(value.state,'error');assert.equal(value.reason,'timeout');assert.equal(signal.aborted,true);
});
test('stopping the host can cancel an in-flight network check without waiting for its timeout',async()=>{
  let signal;const checkerInstance=checker({timeoutMs:10000,fetchImpl:async(url,options)=>{signal=options.signal;return new Promise(()=>{});}});
  const pending=checkerInstance.check();await Promise.resolve();assert.equal(typeof checkerInstance.cancel,'function');checkerInstance.cancel();
  const result=await pending;assert.equal(result.state,'error');assert.equal(result.reason,'cancelled');assert.equal(signal.aborted,true);
});
test('drafts, prereleases, malformed versions, external URLs and incomplete Windows assets are rejected',async()=>{
  for(const change of [x=>x.draft=true,x=>x.prerelease=true,x=>x.tag_name='v1.2.0-rc.1',x=>x.html_url='https://github.com.evil.test/release',x=>x.html_url+='?redirect=evil',x=>x.assets=[],x=>x.assets[0].browser_download_url='file:///C:/bad.exe',x=>x.assets[0].state='new']) {
    const data=release();change(data);const value=await checker({fetchImpl:async()=>reply(data)}).check();assert.equal(value.state,'error');assert.equal(value.reason,'invalid');assert.equal(value.url,null);
  }
});
test('immutable repository identity accepts canonical renamed releases while mismatched identity and owners are rejected',async()=>{
  for(const slug of ['chatgpt-limit-panel','quota-panel-releases','quota-panel-chatgpt','quota-panel-renamed-again']) {
    const result=await checker({fetchImpl:async()=>reply(release('1.2.0',slug))}).check();assert.equal(result.state,'available');assert.equal(result.url,`https://github.com/MrBogomoll/${slug}/releases/tag/v1.2.0`);
  }
  for(const modify of [x=>x.url=x.url.replace('MrBogomoll','attacker'),x=>x.url=x.url.replace('/99','/100'),x=>x.html_url=x.html_url.replace('MrBogomoll','attacker')]){const data=release();modify(data);const result=await checker({fetchImpl:async()=>reply(data)}).check();assert.equal(result.state,'error');assert.equal(result.url,null);}
});
test('MCP update tool works without starting or contacting the monitor and uses the selected language',async()=>{
  const {createMcpHandler}=await import('../plugins/quota-panel/server/mcp.mjs');let calls=0;
  const handle=createMcpHandler({controller:new Proxy({},{get(){throw new Error('monitor lifecycle must not run');}}),getLanguage:()=> 'en',updateChecker:{check:async()=>{calls++;return {state:'available',currentVersion:'1.1.0',latestVersion:'1.2.0',url:origin+'/releases/tag/v1.2.0'};}},readUi:async()=>''});
  const list=(await handle({id:1,method:'tools/list'})).result.tools;const tool=list.find(x=>x.name==='check_quota_panel_updates');assert(tool);assert.equal(tool.annotations.readOnlyHint,true);assert.equal(tool.annotations.openWorldHint,true);
  const result=(await handle({id:2,method:'tools/call',params:{name:tool.name,arguments:{}}})).result;
  assert.equal(calls,1);assert.equal(result.structuredContent.language,'en');assert.equal(result.structuredContent.update.state,'available');assert.match(result.content[0].text,/1.2.0/);
});
