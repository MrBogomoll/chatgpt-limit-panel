import {test} from 'node:test';import assert from 'node:assert/strict';import {spawnSync} from 'node:child_process';import {mkdtemp,rm} from 'node:fs/promises';import {tmpdir} from 'node:os';import path from 'node:path';import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));
test('diagnostic metadata uses one bounded rate-limited background spool without native UI or disk fixtures',{skip:process.platform!=='win32',timeout:15000},async t=>{
  const temp=await mkdtemp(path.join(tmpdir(),'quota-diagnostic-spool-'));t.after(()=>rm(temp,{recursive:true,force:true}));
  const framework=path.join(process.env.WINDIR||'C:/Windows','Microsoft.NET/Framework64/v4.0.30319'),exe=path.join(temp,'diagnostic-spool.exe');
  const built=spawnSync(path.join(framework,'csc.exe'),['/nologo','/codepage:65001','/platform:x64','/target:exe','/main:TaskbarDiagnosticSpoolTest','/out:'+exe,'/reference:'+path.join(framework,'System.Web.Extensions.dll'),path.join(root,'overlay/TaskbarDiagnostics.cs'),path.join(root,'tests/overlay-taskbar-diagnostics-spool.cs')],{encoding:'utf8',windowsHide:true,timeout:10000});
  assert.ifError(built.error);assert.equal(built.status,0,'diagnostic spool compilation failed:\n'+built.stdout+'\n'+built.stderr);
  const result=spawnSync(exe,[],{encoding:'utf8',windowsHide:true,timeout:10000});assert.ifError(result.error);assert.equal(result.status,0,'diagnostic spool failed:\n'+result.stdout+'\n'+result.stderr);assert.equal(result.stderr,'');assert.match(result.stdout,/PASS bounded background diagnostics spool/);t.diagnostic(result.stdout.trim());
});
