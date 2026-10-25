import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));

test('demand-only native task host validates ownership and preserves manual disable without creating Windows tasks', {skip:process.platform!=='win32'}, async t=>{
  const temporary=await mkdtemp(path.join(tmpdir(),'quota task host тест '));
  t.after(()=>rm(temporary,{recursive:true,force:true,maxRetries:10,retryDelay:50}));
  const framework=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319');
  const executable=path.join(temporary,'taskhost-test.exe');
  const compile=spawnSync(path.join(framework,'csc.exe'),['/nologo','/codepage:65001','/platform:x64',`/out:${executable}`,path.join(root,'overlay/TaskHost.cs'),path.join(root,'tests/overlay-taskhost.cs')],{encoding:'utf8',windowsHide:true});
  assert.equal(compile.status,0,compile.stdout+compile.stderr);
  const run=spawnSync(executable,[],{encoding:'utf8',windowsHide:true,timeout:10000});
  assert.equal(run.status,0,run.stdout+run.stderr);
  assert.match(run.stdout,/PASS demand-only/);
});
