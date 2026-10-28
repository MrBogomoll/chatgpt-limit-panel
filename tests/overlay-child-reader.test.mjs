import {test} from 'node:test';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtemp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));
test('native child allocation excludes only its own subtree and preserves foreign controls',{skip:process.platform!=='win32',timeout:30000},async t=>{
  const dir=await mkdtemp(path.join(tmpdir(),'quota child-reader тест '));t.after(()=>rm(dir,{recursive:true,force:true}));
  const framework=path.join(process.env.WINDIR||'C:/Windows','Microsoft.NET/Framework64/v4.0.30319');
  const exe=path.join(dir,'child-reader.exe');
  const refs=['PresentationCore','PresentationFramework','WindowsBase','UIAutomationClient','UIAutomationTypes'].map(n=>path.join(framework,'WPF',n+'.dll'));
  refs.push(path.join(framework,'System.Xaml.dll'),path.join(framework,'System.Web.Extensions.dll'));
  const compiled=spawnSync(path.join(framework,'csc.exe'),['/nologo','/codepage:65001','/platform:x64','/target:exe','/main:ChildReaderTest',`/out:${exe}`,`/win32manifest:${path.join(root,'overlay/app.manifest')}`,...refs.map(p=>'/reference:'+p),...['Theme.cs','TaskbarLayout.cs','TaskbarWindow.cs','TaskbarTransient.cs','TaskbarMenuLease.cs','TaskbarDiagnostics.cs','TaskbarAppearance.cs','TaskbarPassiveOverlay.cs','TaskbarAttachment.cs'].map(n=>path.join(root,'overlay',n)),path.join(root,'tests/overlay-child-reader.cs')],{encoding:'utf8',windowsHide:true,timeout:15000});
  assert.ifError(compiled.error);assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  const result=spawnSync(exe,[],{encoding:'utf8',windowsHide:true,timeout:10000});assert.ifError(result.error);assert.equal(result.status,0,result.stdout+result.stderr);assert.match(result.stdout,/PASS native child reader/);t.diagnostic(result.stdout.trim());
});
