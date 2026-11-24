import { execFile as execFileCallback, spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createWriteStream } from 'node:fs';
import { copyFile, lstat, mkdir, mkdtemp, readFile, realpath, stat, unlink, writeFile } from 'node:fs/promises';
import { homedir, tmpdir } from 'node:os';
import path from 'node:path';
import { pipeline } from 'node:stream/promises';
import { Transform } from 'node:stream';
import { setTimeout as delay } from 'node:timers/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { promisify } from 'node:util';
import { bundledDocuments, installPlugin, payloadFiles, updaterFiles } from './install-plugin.mjs';
import { compareVersions, createUpdateChecker, CURRENT_VERSION } from '../plugins/chatgpt-limit-panel/server/updates.mjs';

const execFile=promisify(execFileCallback);
const market='quota-panel-local', plugin='quota-panel@'+market;
const launcher='dist/overlay/ChatGPTLimitPanelLauncher.exe';
const helper='dist/overlay/ChatGPTLimitPanelUninstaller.exe';
const manifest='.codex-plugin/plugin.json';
const localHome=()=>process.env.CODEX_HOME||path.join(homedir(),'.codex');
const stable=/^\d+\.\d+\.\d+$/;
const versionName=/^\d+\.\d+\.\d+(?:-b\.\d+)?$/;
const sha256=/^[a-f0-9]{64}$/;
const readJson=async file=>JSON.parse((await readFile(file,'utf8')).replace(/^\uFEFF/,''));
const same=(a,b)=>path.resolve(a).toLowerCase()===path.resolve(b).toLowerCase();
const scriptHost=()=>path.join(process.env.SystemRoot||'C:\\Windows','System32','WindowsPowerShell','v1.0','powershell.exe');
const publicDownload=/^https:\/\/github\.com\/MrBogomoll\/[A-Za-z0-9][A-Za-z0-9_.-]{0,99}\/releases\/download\/v?\d+\.\d+\.\d+\//;
const zipName=/^(?:chatgpt-limit-panel|quota-panel)-\d+\.\d+\.\d+-windows-x64\.zip$/;

async function openAsset(asset,fetchImpl,signal) {
  if(!publicDownload.test(asset.url)||!asset.url.endsWith('/'+asset.name)||
    !Number.isSafeInteger(asset.size)||asset.size<1||!sha256.test(asset.digest))throw Error('Invalid release asset');
  let url=new URL(asset.url);
  for(let hops=0;hops<=3;hops++){
    const response=await fetchImpl(url.href,{signal,redirect:'manual',credentials:'omit'});
    if(response.ok&&response.body)return response.body;
    if(![301,302,303,307,308].includes(response.status))throw Error('Release download failed');
    const location=response.headers.get('location');
    await response.body?.cancel().catch(()=>{});
    if(!location)throw Error('Invalid release redirect');
    url=new URL(location,url);
    if(url.protocol!=='https:'||url.port||url.username||url.password||
      !['release-assets.githubusercontent.com','objects.githubusercontent.com'].includes(url.hostname))throw Error('Untrusted release redirect');
  }
  throw Error('Too many release redirects');
}

async function verifiedAsset(asset,fetchImpl,signal,saveTo) {
  const body=await openAsset(asset,fetchImpl,signal);
  const hash=createHash('sha256');let received=0;
  const measure=chunk=>{
    received+=chunk.length;
    if(received>asset.size)throw Error('Release download exceeds expected length');
    hash.update(chunk);
  };
  let data;
  if(saveTo){
    await pipeline(body,new Transform({transform(chunk,_,callback){
      try{measure(chunk);callback(null,chunk);}catch(error){callback(error);}
    }}),createWriteStream(saveTo,{flags:'wx'}),{signal});
  }else{
    const chunks=[];
    for await(const byte of body){const chunk=Buffer.from(byte);measure(chunk);chunks.push(chunk);}
    data=Buffer.concat(chunks);
  }
  if(received!==asset.size||hash.digest('hex')!==asset.digest)throw Error('Release download does not match its SHA-256');
  return data;
}

export async function downloadRelease(assets,directory,fetchImpl=globalThis.fetch) {
  const {zip,checksum}=assets??{};
  if(!zipName.test(zip?.name??'')||checksum?.name!==zip.name+'.sha256'||
    zip.size>96*1024*1024||checksum.size>1024)throw Error('Invalid Windows release manifest');
  const signal=AbortSignal.timeout(90_000);
  const text=await verifiedAsset(checksum,fetchImpl,signal);
  const parts=/^([a-f0-9]{64}) [ *]([a-z0-9._-]+)\r?\n?$/i.exec(text.toString('utf8'));
  if(!parts||parts[1].toLowerCase()!==zip.digest||parts[2]!==zip.name)throw Error('Invalid release checksum');
  await mkdir(directory,{recursive:true});
  const archive=path.join(directory,'verified-panel.zip');
  try{await verifiedAsset(zip,fetchImpl,signal,archive);return archive;}
  catch(error){await unlink(archive).catch(()=>{});throw error;}
}

// Validate names *before* extracting. Even a malformed but checksummed ZIP
// cannot write outside the fresh staging directory or grow without a bound.
export async function extractRelease(archive,stage) {
  await mkdir(stage);
  return execFile(scriptHost(),['-NoLogo','-NoProfile','-NonInteractive','-Command',String.raw`
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$allowed=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$env:QUOTA_UPDATE_FILES.Split('|') | ForEach-Object { [void]$allowed.Add($_) }
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$zip=[IO.Compression.ZipFile]::OpenRead($env:QUOTA_UPDATE_ZIP)
try {
  if($zip.Entries.Count -gt 80){throw 'Archive entry limit'}
  $count=0L
  foreach($entry in $zip.Entries){
    $file=$entry.FullName.Replace('\','/')
    if(-not $allowed.Contains($file) -or -not $seen.Add($file) -or $entry.Length -le 0){throw 'Unexpected archive entry'}
    $count+=$entry.Length
    if($count -gt 160MB){throw 'Expanded archive limit'}
  }
  $buf=New-Object byte[] 65536
  foreach($entry in $zip.Entries){
    $file=$entry.FullName.Replace('\','/')
    $dest=Join-Path $env:QUOTA_UPDATE_STAGE $file
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dest))
    $from=$entry.Open()
    try {
      $to=[IO.File]::Open($dest,[IO.FileMode]::CreateNew)
      try {
        $written=0L
        while(($n=$from.Read($buf,0,$buf.Length)) -gt 0){
          $written+=$n
          if($written -gt $entry.Length){throw 'Invalid archive length'}
          $to.Write($buf,0,$n)
        }
        if($written -ne $entry.Length){throw 'Truncated archive'}
      }finally{$to.Dispose()}
    }finally{$from.Dispose()}
  }
}finally{$zip.Dispose()}
`],{
    windowsHide:true,timeout:35_000,env:{...process.env,
      QUOTA_UPDATE_ZIP:archive,QUOTA_UPDATE_STAGE:stage,
      QUOTA_UPDATE_FILES:[...payloadFiles,...updaterFiles,...bundledDocuments].join('|')},
  });
}

export async function switchInstallation({stopOld,activateNew,startNew,restoreOld,startOld,onRestore}) {
  let changed=false;
  try{
    await stopOld();
    changed=true;
    await activateNew();
    await startNew();
    return 'installed';
  }catch{
    try{
      if(changed)await restoreOld();
      await onRestore?.();
      await startOld();
      return 'restored';
    }catch{return 'failed';}
  }
}

export function managedUpdateRoot(root,home=localHome()) {
  const version=path.basename(root);
  return versionName.test(version)&&same(root,path.join(home,'plugins','cache',market,'quota-panel',version));
}

function codeEnv(home) { return {...process.env,CODEX_HOME:home,NODE_OPTIONS:'',NODE_PATH:''}; }
function execute(executable,args,home,timeout=30_000) {
  return execFile(executable,args,{env:codeEnv(home),windowsHide:true,timeout,encoding:'utf8',maxBuffer:128*1024});
}
async function cli(codex,home,...args) {
  const {stdout}=await execute(codex,['plugin',...args,'--json'],home);
  return JSON.parse(stdout.replace(/^\uFEFF/,''));
}
async function confirmsSource(source,home,version) {
  const parent=path.join(home,'plugins','local-sources');
  if(!source||!same(path.dirname(source),parent)||!path.basename(source).startsWith('chatgpt-limit-panel-')||
    !same(await realpath(source),source)||!same(await realpath(parent),parent))return false;
  const catalog=await readJson(path.join(source,'.agents','plugins','marketplace.json'));
  const packageDir=path.join(source,'plugins','chatgpt-limit-panel');
  const info=await readJson(path.join(packageDir,manifest));
  const receipt=await readJson(path.join(source,'uninstall-inventory.json'));
  return catalog.name===market&&catalog.plugins?.length===1&&catalog.plugins[0].name==='quota-panel'&&
    catalog.plugins[0].source?.source==='local'&&catalog.plugins[0].source?.path==='./plugins/chatgpt-limit-panel'&&
    receipt.schema===1&&receipt.pluginId===plugin&&info.name==='quota-panel'&&info.version===version;
}

async function startPanel(root,home,statePath) {
  const since=Date.now();
  try{await execute(path.join(root,launcher),['--start-json'],home,12_000);}catch{}
  const startup=path.join(path.dirname(statePath),'startup.json');
  for(let i=0;i<35;i++){
    try{
      const info=await readJson(startup), live=await readJson(statePath);
      if(Date.parse(info.time)>=since&&same(info.executable,path.join(root,'dist/overlay/ChatGPTLimitPanel.exe'))&&
        live.process===info.process&&Number.isSafeInteger(live.process)&&live.process>0){
        process.kill(live.process,0);
        return;
      }
    }catch{}
    await delay(300);
  }
  throw Error('Panel startup could not be verified');
}

export async function launchPanelUpdate({root,home=localHome(),codex=process.env.QUOTA_CODEX_BIN,statePath,version}) {
  if(process.platform!=='win32'||!managedUpdateRoot(root,home)||!path.isAbsolute(codex??'')||
    !stable.test(version)||!same(await realpath(root),root))throw Error('Manual update required for this installation');
  const installed=await readJson(path.join(root,manifest));
  if(installed.name!=='quota-panel'||installed.version!==path.basename(root)||
    compareVersions(version,installed.version.replace(/-b\.\d+$/,''))<=0)throw Error('Unexpected update version');
  const directory=await mkdtemp(path.join(await realpath(tmpdir()),'ChatGPTLimitPanel-update-'));
  for(const relative of ['runtime/node.exe',helper,...updaterFiles,'plugins/chatgpt-limit-panel/server/updates.mjs']){
    const source=path.join(root,relative),target=path.join(directory,relative);
    if(!(await lstat(source)).isFile()||!same(await realpath(source),source))throw Error('Invalid update helper');
    await mkdir(path.dirname(target),{recursive:true});
    await copyFile(source,target);
  }
  const job=path.join(directory,'job.json');
  await writeFile(job,JSON.stringify({root,home,codex,statePath,version}),{flag:'wx'});
  const child=spawn(path.join(directory,'runtime/node.exe'),[path.join(directory,'overlay/updater.mjs'),'--worker',job],
    {cwd:directory,detached:true,windowsHide:true,stdio:'ignore',env:codeEnv(home)});
  return new Promise((resolve,reject)=>{
    child.on('error',reject);
    child.on('spawn',()=>child.unref());
    child.on('exit',code=>code===0?resolve():reject(Error('Update could not complete')));
  });
}

const recoveryFile=statePath=>path.join(path.dirname(statePath),'update-outcome.json');
export async function takeUpdateOutcome(statePath) {
  const file=recoveryFile(statePath);
  try{
    const result=await readJson(file);
    await unlink(file);
    if(result.status==='restored'&&Number.isFinite(result.at)&&Date.now()-result.at<120_000)return result.status;
  }catch{}
  return null;
}

async function work({root,home,codex,statePath,version},directory) {
  if(!managedUpdateRoot(root,home)||!stable.test(version)||!path.isAbsolute(codex))throw Error('Invalid update job');
  const oldVersion=path.basename(root);
  const listings=await cli(codex,home,'list','--marketplace',market);
  if(listings.installed?.length!==1||!listings.installed.some(item=>item.pluginId===plugin&&item.version===oldVersion&&item.installed&&item.enabled))
    throw Error('Unrecognized plugin registration');
  const source=async()=> (await cli(codex,home,'marketplace','list')).marketplaces?.find(item=>item.name===market)?.root;
  const oldSource=await source();
  if(!await confirmsSource(oldSource,home,oldVersion))throw Error('Unknown plugin source');
  const release=await createUpdateChecker({currentVersion:oldVersion,includeInstallAssets:true}).check();
  if(release.state!=='available'||release.latestVersion!==version||!release.installAssets)throw Error('Release is no longer available');
  const archive=await downloadRelease(release.installAssets,directory);
  const stage=path.join(directory,'stage');
  await extractRelease(archive,stage);
  const prepared=installPlugin({stagePath:stage,codexHome:home,stageOnly:true,expectedVersion:version});
  const nextRoot=path.join(path.dirname(root),version);
  const unlock=target=>execute(path.join(directory,helper),['--prepare-update','--root',target,'--home',home],home,36_000);
  const register=async target=>{
    const current=await source();
    if(current&&!same(current,oldSource)&&!same(current,prepared.marketplaceRoot))throw Error('Plugin source changed during update');
    if(current&&!same(current,target))await cli(codex,home,'marketplace','remove',market);
    if(!current||!same(current,target))await cli(codex,home,'marketplace','add',target);
    await cli(codex,home,'add',plugin);
  };
  const result=await switchInstallation({
    stopOld:()=>unlock(root),
    activateNew:()=>register(prepared.marketplaceRoot),
    startNew:async()=>{
      const list=await cli(codex,home,'list','--marketplace',market);
      if(!list.installed?.some(item=>item.pluginId===plugin&&item.version===version&&item.installed&&item.enabled))
        throw Error('New installation was not activated');
      await startPanel(nextRoot,home,statePath);
    },
    restoreOld:async()=>{
      if(await stat(path.join(nextRoot,launcher)).catch(()=>null))await unlock(nextRoot).catch(()=>{});
      await register(oldSource);
    },
    startOld:()=>startPanel(root,home,statePath),
    onRestore:()=>writeFile(recoveryFile(statePath),JSON.stringify({status:'restored',at:Date.now()})),
  });
  if(result!=='installed')throw Error('Update did not install');
}

function cleanup(directory) {
  // Windows keeps the worker's own executable open until it exits.
  const child=spawn(scriptHost(),['-NoLogo','-NoProfile','-NonInteractive','-Command',
    'Wait-Process -Id ([int]$env:QUOTA_UPDATE_PID) -ErrorAction SilentlyContinue; Remove-Item -LiteralPath $env:QUOTA_UPDATE_TEMP -Recurse -Force -ErrorAction SilentlyContinue'],
  {cwd:tmpdir(),detached:true,windowsHide:true,stdio:'ignore',env:{...process.env,QUOTA_UPDATE_PID:String(process.pid),QUOTA_UPDATE_TEMP:directory}});
  child.on('error',()=>{});child.unref();
}

if(process.argv[1]&&pathToFileURL(path.resolve(process.argv[1])).href===import.meta.url&&process.argv[2]==='--worker'){
  const job=path.resolve(process.argv[3]??'');
  const directory=path.dirname(job);
  let owned=false;
  try{
    owned=process.platform==='win32'&&same(path.dirname(directory),await realpath(tmpdir()))&&
      /^ChatGPTLimitPanel-update-[A-Za-z0-9]+$/.test(path.basename(directory))&&
      same(process.execPath,path.join(directory,'runtime/node.exe'))&&
      path.basename(job)==='job.json'&&same(await realpath(directory),directory);
    if(!owned)throw Error('Invalid updater worker directory');
    await work(await readJson(job),directory);
  }catch{process.exitCode=1;}
  finally{if(owned)cleanup(directory);}
}
