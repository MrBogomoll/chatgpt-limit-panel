import {test} from 'node:test';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtemp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));
test('a recognized shell menu retains only an already visible verified footprint while native evidence remains unchanged',{skip:process.platform!=='win32',timeout:15000},async t=>{
  const temporary=await mkdtemp(path.join(tmpdir(),'quota-menu-lease-'));t.after(()=>rm(temporary,{recursive:true,force:true}));
  const compiler=path.join(process.env.WINDIR||'C:/Windows','Microsoft.NET/Framework64/v4.0.30319/csc.exe');const executable=path.join(temporary,'menu-lease-test.exe');
  const built=spawnSync(compiler,['/nologo','/codepage:65001','/platform:x64','/target:exe','/main:TaskbarMenuLeaseTest','/out:'+executable,...['overlay/TaskbarLayout.cs','overlay/TaskbarMenuLease.cs','tests/overlay-taskbar-menu-lease.cs'].map(x=>path.join(root,x))],{encoding:'utf8',windowsHide:true,timeout:10000});
  assert.ifError(built.error);assert.equal(built.status,0,'menu lease test compilation failed:\n'+built.stdout+'\n'+built.stderr);
  const result=spawnSync(executable,[],{encoding:'utf8',windowsHide:true,timeout:5000});assert.ifError(result.error);assert.equal(result.status,0,'menu lease behavior failed:\n'+result.stdout+'\n'+result.stderr);assert.equal(result.stderr,'');assert.match(result.stdout,/PASS fixed-footprint menu lease/);t.diagnostic(result.stdout.trim());
});
