import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, rm, writeFile, readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));
test('native launcher resolves portable paths and reports actual own mutex rather than stale state', {skip:process.platform!=='win32'},async t=>{
  const temporary=await mkdtemp(path.join(tmpdir(),'quota launcher тест '));
  t.after(()=>rm(temporary,{recursive:true,force:true,maxRetries:10,retryDelay:50}));
  const framework=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319');
  const executable=path.join(temporary,'launcher-test.exe');
  const compile=spawnSync(path.join(framework,'csc.exe'),['/nologo','/codepage:65001','/platform:x64','/main:OverlayLauncherTest',`/out:${executable}`,`/reference:${framework}/System.Web.Extensions.dll`,`/reference:${framework}/System.Windows.Forms.dll`,path.join(root,'overlay/Launcher.cs'),path.join(root,'overlay/TaskHost.cs'),path.join(root,'overlay/Preferences.cs'),path.join(root,'tests/overlay-launcher.cs')],{encoding:'utf8',windowsHide:true});
  assert.equal(compile.status,0,compile.stdout+compile.stderr);
  const run=spawnSync(executable,[temporary],{encoding:'utf8',windowsHide:true,timeout:10000});
  assert.equal(run.status,0,run.stdout+run.stderr);
  assert.match(run.stdout,/PASS launcher/);
  await writeFile(path.join(temporary,'background.cjs'),`require('fs').writeFileSync(process.argv[3],JSON.stringify({pid:process.pid}));setTimeout(()=>process.exit(0),3000);`);
  const detached=spawnSync(executable,['--pipe',temporary,process.execPath],{encoding:'utf8',windowsHide:true,timeout:1500});
  assert.equal(detached.status,0,'long-running host must not keep the launcher caller pipes open: '+detached.stderr);
  assert.match(detached.stdout,/PASS detached/);
  let child;
  for(let attempt=0;attempt<30;attempt++) {
    try{child=JSON.parse(await readFile(path.join(temporary,'child.json'),'utf8'));break;}catch{await new Promise(resolve=>setTimeout(resolve,20));}
  }
  assert(child?.pid,'detached host must actually start');
  try{process.kill(child.pid,0);}finally{try{process.kill(child.pid);}catch{}}
});
