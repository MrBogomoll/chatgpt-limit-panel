import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
test('portable logon autostart migrates only owned per-user tasks without changing Windows during tests',{skip:process.platform!=='win32'},async t=>{
 const root=process.cwd(),dir=await mkdtemp(path.join(tmpdir(),'quota autostart тест '));t.after(()=>rm(dir,{recursive:true,force:true,maxRetries:10,retryDelay:50}));
 const fw=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319'),exe=path.join(dir,'test.exe');
 const compiled=spawnSync(path.join(fw,'csc.exe'),['/nologo','/codepage:65001',`/out:${exe}`,path.join(root,'overlay/TaskHost.cs'),path.join(root,'tests/overlay-autostart.cs')],{encoding:'utf8',windowsHide:true});
 assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);const result=spawnSync(exe,[],{encoding:'utf8',windowsHide:true,timeout:10000});assert.equal(result.status,0,result.stdout+result.stderr);assert.match(result.stdout,/PASS portable autostart/);
});
