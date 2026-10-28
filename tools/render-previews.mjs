import {spawnSync} from 'node:child_process';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url)),temporary=path.join(root,'dist/documentation-previews');mkdirSync(temporary,{recursive:true});
const framework=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319');
const overlay=path.join(temporary,'PreviewOverlay.cs');writeFileSync(overlay,readFileSync(path.join(root,'overlay/Overlay.cs'),'utf8').replaceAll('Local\\\\ChatGPTQuotaOverlay.Stop','Local\\\\ChatGPTLimitPanel.DocumentationPreview.Stop'));
const sources=[...new Set([...readFileSync(path.join(root,'overlay/build.mjs'),'utf8').matchAll(/path\.join\(root,'(overlay\/[^']+\.cs)'\)/g)].map(m=>m[1]))].map(name=>name==='overlay/Overlay.cs'?overlay:path.join(root,name));
const refs=[...['PresentationCore','PresentationFramework','WindowsBase','UIAutomationClient','UIAutomationTypes'].map(n=>path.join(framework,'WPF',n+'.dll')),...['System.Web.Extensions','System.Windows.Forms','System.Drawing','System.Xaml'].map(n=>path.join(framework,n+'.dll'))];
const exe=path.join(temporary,'DocumentationPreviews.exe');
function run(file,args){const r=spawnSync(file,args,{cwd:root,encoding:'utf8',windowsHide:true,timeout:30000,env:{...process.env,QUOTA_PANEL_DIAGNOSTICS:'0'}});if(r.status!==0)throw Error(r.stdout+r.stderr);if(r.stdout)process.stdout.write(r.stdout);}
run(path.join(framework,'csc.exe'),['/nologo','/codepage:65001','/platform:x64','/target:exe','/main:DocumentationPreviews','/out:'+exe,...refs.map(n=>'/reference:'+n),...sources,path.join(root,'tools/render-previews.cs')]);
run(exe,[path.join(root,'images'),path.join(temporary,'state')]);
