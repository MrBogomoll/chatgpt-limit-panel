import {test} from 'node:test';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtemp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));
test('modern slider retains theme bindings, value events and the clear panel input surface',{skip:process.platform!=='win32',timeout:15000},async t=>{
  const temporary=await mkdtemp(path.join(tmpdir(),'quota-appearance-'));t.after(()=>rm(temporary,{recursive:true,force:true}));
  const framework=path.join(process.env.WINDIR||'C:/Windows','Microsoft.NET/Framework64/v4.0.30319');const executable=path.join(temporary,'appearance-test.exe');
  const references=['PresentationCore','PresentationFramework','WindowsBase'].map(n=>'/reference:'+path.join(framework,'WPF',n+'.dll'));references.push('/reference:'+path.join(framework,'System.Xaml.dll'));
  const built=spawnSync(path.join(framework,'csc.exe'),['/nologo','/codepage:65001','/platform:x64','/target:exe','/main:TaskbarAppearanceTest','/out:'+executable,...references,path.join(root,'overlay/Theme.cs'),path.join(root,'overlay/TaskbarAppearance.cs'),path.join(root,'tests/overlay-appearance.cs')],{encoding:'utf8',windowsHide:true,timeout:10000});
  assert.ifError(built.error);assert.equal(built.status,0,'appearance compilation failed:\n'+built.stdout+'\n'+built.stderr);
  const output=process.env.QUOTA_APPEARANCE_PREVIEW_DIR||temporary;const result=spawnSync(executable,[output],{encoding:'utf8',windowsHide:true,timeout:5000});
  assert.ifError(result.error);assert.equal(result.status,0,'appearance check failed:\n'+result.stdout+'\n'+result.stderr);assert.equal(result.stderr,'');assert.match(result.stdout,/PASS modern slider and transparent input surface/);t.diagnostic(result.stdout.trim());
});
