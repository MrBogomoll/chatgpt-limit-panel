import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const root=fileURLToPath(new URL('../',import.meta.url));
test('native attachment changes only own cross-process fake windows and rejects unsafe boundaries',{skip:process.platform!=='win32',timeout:30000},async t=>{
  const temporary=await mkdtemp(path.join(tmpdir(),'quota native child тест '));
  t.after(()=>rm(temporary,{recursive:true,force:true}));
  const framework=path.join(process.env.WINDIR||'C:/Windows','Microsoft.NET/Framework64/v4.0.30319');
  const executable=path.join(temporary,'attachment-test.exe');
  const result=spawnSync(path.join(framework,'csc.exe'),[
    '/nologo','/codepage:65001','/platform:x64','/target:exe','/main:AttachmentNativeTest','/out:'+executable,
    '/win32manifest:'+path.join(root,'overlay/app.manifest'),
    ...['PresentationCore','PresentationFramework','WindowsBase'].map(name=>'/reference:'+path.join(framework,'WPF',name+'.dll')),
    '/reference:'+path.join(framework,'System.Xaml.dll'),'/reference:'+path.join(framework,'System.Web.Extensions.dll'),
    path.join(root,'overlay/TaskbarLayout.cs'),path.join(root,'overlay/TaskbarAttachment.cs'),path.join(root,'tests/overlay-attachment.cs'),
  ],{encoding:'utf8',windowsHide:true,timeout:15000});
  assert.ifError(result.error);assert.equal(result.status,0,`attachment compilation failed:\n${result.stdout}\n${result.stderr}`);
  const run=spawnSync(executable,[temporary],{encoding:'utf8',windowsHide:true,timeout:15000});
  assert.ifError(run.error);assert.equal(run.status,0,`attachment boundary failed:\n${run.stdout}\n${run.stderr}`);
  assert.match(run.stdout,/PASS native attachment/);t.diagnostic(run.stdout.trim());
});
