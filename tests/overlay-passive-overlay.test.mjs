import {test} from 'node:test';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync} from 'node:fs';
import {mkdtemp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));

test('passive overlay policy requires the exact protected disabled NVIDIA canvas and preserves foreground', {skip:process.platform!=='win32',timeout:20000}, async t=>{
  const temporary=await mkdtemp(path.join(tmpdir(),'quota passive overlay '));
  t.after(()=>rm(temporary,{recursive:true,force:true}));
  const framework=path.join(process.env.WINDIR||'C:/Windows','Microsoft.NET/Framework64/v4.0.30319');
  const executable=path.join(temporary,'passive-overlay-test.exe');
  const policy=path.join(root,'overlay/TaskbarPassiveOverlay.cs');
  const sources=[path.join(root,'tests/overlay-passive-overlay.cs')];
  // The missing-policy case still compiles and fails at the behavioral entry
  // point, so RED identifies the absent feature rather than a compiler error.
  if(existsSync(policy))sources.unshift(policy);
  const built=spawnSync(path.join(framework,'csc.exe'),[
    '/nologo','/codepage:65001','/platform:x64','/target:exe','/main:TaskbarPassiveOverlayTest',`/out:${executable}`,
    `/reference:${path.join(framework,'System.Windows.Forms.dll')}`,`/reference:${path.join(framework,'System.Drawing.dll')}`,...sources,
  ],{encoding:'utf8',windowsHide:true,timeout:12000});
  assert.ifError(built.error);assert.equal(built.status,0,`compile failed:\n${built.stdout}\n${built.stderr}`);
  const result=spawnSync(executable,[],{encoding:'utf8',windowsHide:true,timeout:5000});
  assert.ifError(result.error);assert.equal(result.status,0,`passive overlay behavior failed:\n${result.stdout}\n${result.stderr}`);
  assert.equal(result.stderr,'');assert.match(result.stdout,/PASS passive overlay policy/);t.diagnostic(result.stdout.trim());
});
