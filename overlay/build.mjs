import { spawnSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));
const framework=path.join(process.env.WINDIR||'C:/Windows','Microsoft.NET/Framework64/v4.0.30319');
const out=path.join(root,'dist/overlay'); mkdirSync(out,{recursive:true});
const test=process.argv.includes('--test-layout');
const args=['/nologo','/codepage:65001','/platform:x64',`/out:${path.join(out,test?'layout-test.exe':'QuotaOverlay.exe')}`];
if(test) args.push(path.join(root,'overlay/Layout.cs'),path.join(root,'tests/overlay-layout.cs'));
else {
  args.push('/target:winexe',`/win32manifest:${path.join(root,'overlay/app.manifest')}`);
  for(const name of ['PresentationCore','PresentationFramework','WindowsBase','UIAutomationClient','UIAutomationTypes']) args.push(`/reference:${path.join(framework,'WPF',name+'.dll')}`);
  for(const name of ['System.Web.Extensions','System.Windows.Forms','System.Drawing','System.Xaml']) args.push(`/reference:${path.join(framework,name+'.dll')}`);
  args.push(path.join(root,'overlay/Layout.cs'),path.join(root,'overlay/TaskbarLayout.cs'),path.join(root,'overlay/TaskbarWindow.cs'),path.join(root,'overlay/TaskbarChildSurface.cs'),path.join(root,'overlay/TaskbarTransient.cs'),path.join(root,'overlay/TaskbarMenuLease.cs'),path.join(root,'overlay/TaskbarDiagnostics.cs'),path.join(root,'overlay/TaskbarAppearance.cs'),path.join(root,'overlay/TaskbarAttachment.cs'),path.join(root,'overlay/TaskbarPassiveOverlay.cs'),path.join(root,'overlay/Progress.cs'),path.join(root,'overlay/VisualHost.cs'),path.join(root,'overlay/Theme.cs'),path.join(root,'overlay/Preferences.cs'),path.join(root,'overlay/Language.cs'),path.join(root,'overlay/Overlay.cs'));
}
const result=spawnSync(path.join(framework,'csc.exe'),args,{encoding:'utf8',windowsHide:true});
if(result.stdout)process.stdout.write(result.stdout);if(result.stderr)process.stderr.write(result.stderr);
if(result.error)throw result.error;
process.exitCode=result.status??1;
if(!test && result.status===0) {
  const launcher=spawnSync(path.join(framework,'csc.exe'),['/nologo','/codepage:65001','/platform:x64','/target:winexe',`/out:${path.join(out,'QuotaMonitor.exe')}`,`/reference:${path.join(framework,'System.Web.Extensions.dll')}`,`/reference:${path.join(framework,'System.Windows.Forms.dll')}`,path.join(root,'overlay/Launcher.cs'),path.join(root,'overlay/TaskHost.cs'),path.join(root,'overlay/Preferences.cs'),path.join(root,'overlay/Language.cs')],{encoding:'utf8',windowsHide:true});
  if(launcher.stdout)process.stdout.write(launcher.stdout);if(launcher.stderr)process.stderr.write(launcher.stderr);
  if(launcher.error)throw launcher.error;process.exitCode=launcher.status??1;
  const codex=process.env.QUOTA_CODEX_BIN||process.env.CODEX_CLI_PATH||'codex';
  writeFileSync(path.join(out,'runtime.json'),JSON.stringify({node:process.execPath,host:path.join(root,'overlay/host.mjs'),codex},null,2));
}
