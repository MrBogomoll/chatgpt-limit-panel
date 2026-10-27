import {test} from 'node:test';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtemp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));
test('taskbar transient policy recognizes only protected Windows capture hosts and native menus', {skip:process.platform!=='win32',timeout:20000}, async t=>{
  const temporary=await mkdtemp(path.join(tmpdir(),'quota taskbar transient '));
  t.after(()=>rm(temporary,{recursive:true,force:true}));
  const framework=path.join(process.env.WINDIR||'C:/Windows','Microsoft.NET/Framework64/v4.0.30319');
  const executable=path.join(temporary,'taskbar-transient-test.exe');
  const built=spawnSync(path.join(framework,'csc.exe'),[
    '/nologo','/codepage:65001','/platform:x64','/target:exe','/main:TaskbarTransientTest',`/out:${executable}`,
    `/reference:${path.join(framework,'System.Windows.Forms.dll')}`,`/reference:${path.join(framework,'System.Drawing.dll')}`,
    path.join(root,'overlay/TaskbarTransient.cs'),path.join(root,'tests/overlay-taskbar-transient.cs'),
  ],{encoding:'utf8',windowsHide:true,timeout:12000});
  assert.ifError(built.error);assert.equal(built.status,0,`compile failed:\n${built.stdout}\n${built.stderr}`);
  const result=spawnSync(executable,[],{encoding:'utf8',windowsHide:true,timeout:5000});
  assert.ifError(result.error);assert.equal(result.status,0,`transient behavior failed:\n${result.stdout}\n${result.stderr}`);
  assert.equal(result.stderr,'');assert.match(result.stdout,/PASS taskbar transient policy/);t.diagnostic(result.stdout.trim());
});
