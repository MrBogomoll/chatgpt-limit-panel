import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { downloadRelease, switchInstallation } from '../overlay/updater.mjs';

test('only a complete verified release archive can be staged', async t => {
  const folder=await mkdtemp(path.join(tmpdir(),'quota update fixture '));
  t.after(()=>rm(folder,{recursive:true,force:true}));
  const zip=Buffer.from([0x50,0x4b,0x03,0x04,1,2,3]);
  const sha=bytes=>createHash('sha256').update(bytes).digest('hex');
  const name='chatgpt-limit-panel-1.8.4-windows-x64.zip';
  const url='https://github.com/MrBogomoll/chatgpt-limit-panel/releases/download/v1.8.4/';
  const text=Buffer.from(sha(zip)+'  '+name+'\n');
  const asset=(name,data)=>({name,url:url+name,size:data.length,digest:sha(data)});
  const assets={zip:asset(name,zip),checksum:asset(name+'.sha256',text)};
  const fetchImpl=async address=>address.endsWith('.sha256')?new Response(text):new Response(zip);
  assert.deepEqual(await readFile(await downloadRelease(assets,folder,fetchImpl)),zip);
  const refused=path.join(folder,'unverified');
  await assert.rejects(downloadRelease({...assets,zip:{...assets.zip,digest:'a'.repeat(64)}},refused,fetchImpl));
  await assert.rejects(downloadRelease(assets,refused,address=>address.endsWith('.sha256')?new Response(text):new Response(Buffer.alloc(zip.length))),/SHA-256/i);
  await assert.rejects(downloadRelease(assets,refused,async()=>new Response(null,{status:302,headers:{Location:'https://example.com/private'}})),/redirect/i);
});

test('a failed new launch rolls back source and resumes only the previous panel', async () => {
  const calls=[];
  const result=await switchInstallation({
    stopOld:async()=>calls.push('stop old panel'),
    activateNew:async()=>calls.push('activate new panel'),
    startNew:async()=>{calls.push('check new panel');throw Error('did not start');},
    restoreOld:async()=>calls.push('restore old registration'),
    startOld:async()=>calls.push('start old panel'),
  });
  assert.equal(result,'restored');
  assert.deepEqual(calls,['stop old panel','activate new panel','check new panel','restore old registration','start old panel']);
});
