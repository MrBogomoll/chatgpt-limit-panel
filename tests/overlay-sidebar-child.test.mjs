import {test} from 'node:test';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtemp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
test('sidebar visual survives parent recreation and retains transparent input without touching user windows',{skip:process.platform!=='win32'},async t=>{
 const root=fileURLToPath(new URL('../',import.meta.url)),temporary=await mkdtemp(path.join(tmpdir(),'limit-panel-sidebar-'));
 t.after(()=>rm(temporary,{recursive:true,force:true}));
 const framework=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319'),exe=path.join(temporary,'sidebar.exe');
 const refs=[...['PresentationCore','PresentationFramework','WindowsBase','UIAutomationClient','UIAutomationTypes'].map(n=>path.join(framework,'WPF',n+'.dll')),...['System.Xaml','System.Web.Extensions'].map(n=>path.join(framework,n+'.dll'))];
 const sources=['Theme.cs','TaskbarLayout.cs','TaskbarAttachment.cs','TaskbarChildSurface.cs','TaskbarAppearance.cs','VisualHost.cs'].map(n=>path.join(root,'overlay',n));
 const built=spawnSync(path.join(framework,'csc.exe'),['/nologo','/codepage:65001','/platform:x64','/target:exe','/main:SidebarChildTest','/out:'+exe,'/win32manifest:'+path.join(root,'overlay/app.manifest'),...refs.map(n=>'/reference:'+n),...sources,path.join(root,'tests/overlay-sidebar-child.cs')],{encoding:'utf8',windowsHide:true,timeout:15000});
 assert.equal(built.status,0,built.stdout+built.stderr);
 const result=spawnSync(exe,[],{encoding:'utf8',windowsHide:true,timeout:15000});
 assert.equal(result.status,0,result.stdout+result.stderr);assert.match(result.stdout,/PASS isolated sidebar lifetime\/render\/input/);t.diagnostic(result.stdout.trim());
});
