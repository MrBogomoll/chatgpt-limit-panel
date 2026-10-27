import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root=fileURLToPath(new URL('../',import.meta.url));
test('all four taskbar edges allocate safe upright percentage labels without touching the live shell', {skip:process.platform!=='win32',timeout:20000}, async t=>{
  const temporary=await mkdtemp(path.join(tmpdir(),'quota edges тест '));
  t.after(()=>rm(temporary,{recursive:true,force:true}));
  const executable=path.join(temporary,'taskbar-edges.exe');
  const compiler=path.join(process.env.WINDIR||'C:/Windows','Microsoft.NET/Framework64/v4.0.30319/csc.exe');
  const compiled=spawnSync(compiler,['/nologo','/codepage:65001','/platform:x64','/target:exe','/main:TaskbarEdgesTest',`/out:${executable}`,path.join(root,'overlay/TaskbarLayout.cs'),path.join(root,'tests/overlay-taskbar-edges.cs')],{encoding:'utf8',windowsHide:true,timeout:10000});
  assert.ifError(compiled.error);assert.equal(compiled.status,0,compiled.stdout+'\n'+compiled.stderr);
  const result=spawnSync(executable,[],{encoding:'utf8',windowsHide:true,timeout:5000});
  assert.ifError(result.error);assert.equal(result.status,0,result.stdout+'\n'+result.stderr);assert.equal(result.stderr,'');assert.match(result.stdout,/PASS four taskbar edges/);t.diagnostic(result.stdout.trim());
});
