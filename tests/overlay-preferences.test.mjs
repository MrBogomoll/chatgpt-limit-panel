import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,rm,writeFile,readFile,mkdir,access} from 'node:fs/promises';
import {spawn,spawnSync} from 'node:child_process';
import {tmpdir} from 'node:os';
import path from 'node:path';
test('GUI and startup settings preserve each other during concurrent native updates',{skip:process.platform!=='win32'},async t=>{
 const root=process.cwd(),dir=await mkdtemp(path.join(tmpdir(),'quota settings тест '));t.after(()=>rm(dir,{recursive:true,force:true,maxRetries:10,retryDelay:50}));
 const fw=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319'),exe=path.join(dir,'preferences.exe'),file=path.join(dir,'settings.json');
 const compiled=spawnSync(path.join(fw,'csc.exe'),['/nologo','/codepage:65001',`/reference:${fw}/System.Web.Extensions.dll`,`/out:${exe}`,path.join(root,'overlay/Preferences.cs'),path.join(root,'tests/overlay-preferences.cs')],{encoding:'utf8',windowsHide:true});assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
 await writeFile(file,JSON.stringify({other:'keep',displayMode:'compact'}));
 await Promise.all(['theme','autostart'].map(kind=>new Promise((resolve,reject)=>{const p=spawn(exe,[file,kind],{windowsHide:true,stdio:['ignore','ignore','pipe']});let error='';p.stderr.on('data',b=>error+=b);p.on('error',reject);p.on('exit',code=>code===0?resolve():reject(new Error(error)));})));
 assert.deepEqual(JSON.parse((await readFile(file,'utf8')).replace(/^\uFEFF/,'')),{other:'keep',displayMode:'compact',themeMode:'light',autoStartEnabled:false});
 const run=kind=>new Promise((resolve,reject)=>{const p=spawn(exe,[file,kind],{windowsHide:true,stdio:['ignore','ignore','pipe']});let error='';p.stderr.on('data',b=>error+=b);p.on('error',reject);p.on('exit',code=>code===0?resolve():reject(new Error(error)));});
 await writeFile(file,JSON.stringify({autoStartEnabled:true,other:'keep'}));
 const start=run('control-start');
 for(let i=0;i<100;i++){try{await readFile(file+'.read');break;}catch{await new Promise(r=>setTimeout(r,10));}}
 await Promise.all([start,run('control-disable')]);
 assert.equal(JSON.parse((await readFile(file,'utf8')).replace(/^\uFEFF/,'')).autoStartEnabled,false,'a concurrent explicit disable must not be undone by a stale Start read');
 assert.equal(await readFile(file+'.task','utf8'),'false','scheduled trigger and preference must agree');
 const local=path.join(dir,'local'),legacy=path.join(local,'ChatGPTQuotaOverlay'),current=path.join(local,'ChatGPTLimitPanel');await mkdir(legacy,{recursive:true});
 const saved='{"language":"en","themeMode":"light","autoStartEnabled":false}';await writeFile(path.join(legacy,'settings.json'),saved);await writeFile(path.join(legacy,'state.json'),'preserve diagnostics');
 const data=kind=>{const r=spawnSync(exe,[local,kind],{windowsHide:true,encoding:'utf8'});assert.equal(r.status,0,r.stderr);return r.stdout;};
 assert.equal(data('data-read'),legacy);await assert.rejects(access(current));
 assert.equal(data('data-migrate'),current);await assert.rejects(access(legacy));assert.equal(await readFile(path.join(current,'settings.json'),'utf8'),saved);assert.equal(await readFile(path.join(current,'state.json'),'utf8'),'preserve diagnostics');
 assert.equal(data('data-migrate'),current,'migration is idempotent');await mkdir(legacy);await writeFile(path.join(legacy,'settings.json'),'legacy conflict');
 assert.equal(data('data-migrate'),current);assert.equal(await readFile(path.join(current,'settings.json'),'utf8'),saved,'existing destination is never overwritten');assert.equal(await readFile(path.join(legacy,'settings.json'),'utf8'),'legacy conflict');
});
