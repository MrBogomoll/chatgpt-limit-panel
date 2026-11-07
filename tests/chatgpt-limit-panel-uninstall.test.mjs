import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,mkdir,writeFile,readFile,rm,readdir,realpath,rename,symlink} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {createMcpHandler} from '../plugins/chatgpt-limit-panel/server/mcp.mjs';
const api=await import('../plugins/chatgpt-limit-panel/server/uninstall.mjs').catch(()=>({}));

test('open uninstaller copies its helper outside the package and only opens confirmation',async t=>{
 assert.equal(typeof api.createUninstaller,'function','a detached confirmation launcher must exist');
 const temporary=await mkdtemp(path.join(tmpdir(),'quota-uninstall-open-'));t.after(()=>rm(temporary,{recursive:true,force:true}));
 const pluginRoot=path.join(temporary,'home/plugins/cache/quota-panel-local/quota-panel/1.7.6'),tempRoot=path.join(temporary,'temp');
 await mkdir(path.join(pluginRoot,'dist/overlay'),{recursive:true});await mkdir(tempRoot);
 const helper=path.join(pluginRoot,'dist/overlay/ChatGPTLimitPanelUninstaller.exe');await writeFile(helper,'test helper');
 await mkdir(path.join(pluginRoot,'.codex-plugin'));await writeFile(path.join(pluginRoot,'.codex-plugin/plugin.json'),JSON.stringify({name:'quota-panel',version:'1.7.6',interface:{displayName:'ChatGPT Limit Panel'}}));
 const calls=[];const open=api.createUninstaller({pluginRoot,codexHome:path.join(temporary,'home'),tempRoot,launch:async(file,args,options)=>calls.push({file,args,options})});
 const result=await open.open('en');assert.equal(result.opened,true);assert.equal(calls.length,1);
 assert.notEqual(calls[0].file,helper);assert.equal(await readFile(calls[0].file,'utf8'),'test helper');
 assert.deepEqual(calls[0].args,['--root',await realpath(pluginRoot),'--home',await realpath(path.join(temporary,'home')),'--language','en']);assert.equal(calls[0].options.cwd,path.dirname(calls[0].file),'helper must not hold the installed package as its working directory');
 assert.equal(calls[0].options.detached,true);assert.equal(calls[0].options.windowsHide,false,'the explicit confirmation window must be visible');assert.equal(calls[0].options.shell,false);assert.equal(calls[0].options.stdio,'ignore');
 assert.equal(await readFile(helper,'utf8'),'test helper','opening must not modify the package or remove anything');
 const failed=api.createUninstaller({pluginRoot,codexHome:path.join(temporary,'home'),tempRoot,launch:async()=>{throw Error('private filesystem path');}});
 await assert.rejects(failed.open('ru'),error=>!/private filesystem path/.test(error.message));
 assert.equal((await readdir(tempRoot)).length,1,'failed opening cleans only its own temporary copy');
 const redirected=path.join(temporary,'redirected-overlay');await rename(path.dirname(helper),redirected);await symlink(redirected,path.dirname(helper),'junction');
 await assert.rejects(open.open('ru'));assert.equal(calls.length,1,'a redirected ancestor must be rejected before any program is launched');
 await rm(path.dirname(helper));await rename(redirected,path.dirname(helper));
 const manifestDirectory=path.join(pluginRoot,'.codex-plugin'),externalManifest=path.join(temporary,'redirected-manifest');await rename(manifestDirectory,externalManifest);await symlink(externalManifest,manifestDirectory,'junction');
 await assert.rejects(open.open('ru'));assert.equal(calls.length,1,'a redirected ownership manifest cannot authorize opening a program');
});

test('uninstall entry is app-only, opens confirmation and rejects arguments without changing monitor state',async()=>{
 let opens=0,stops=0;const handler=createMcpHandler({controller:{openUninstaller:async language=>{opens++;return {opened:true,language,message:'Confirmation opened'};},stop:async()=>{stops++;}},readUi:async()=>'',getLanguage:()=> 'en'});
 const listing=await handler({id:1,method:'tools/list'});const tool=listing.result.tools.find(t=>t.name==='open_quota_panel_uninstaller');
 assert(tool,'the card needs an explicit confirmation opener');assert.deepEqual(tool._meta.ui.visibility,['app']);assert.equal(tool.annotations.destructiveHint,false,'opening confirmation is not deletion');
 const opened=await handler({id:2,method:'tools/call',params:{name:tool.name,arguments:{}}});assert.equal(opened.result.structuredContent.opened,true);assert.equal(opened.result.structuredContent.language,'en');assert.equal(opens,1);assert.equal(stops,0);
 const wrong=await handler({id:3,method:'tools/call',params:{name:tool.name,arguments:{confirm:true,deleteSettings:true}}});assert.equal(wrong.error.code,-32602);assert.equal(opens,1);
});
