import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, rm, writeFile, copyFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const controllerApi = await import('../plugins/quota-panel/server/overlay-controller.mjs').catch(() => ({}));
const mcpApi = await import('../plugins/quota-panel/server/mcp.mjs').catch(() => ({}));
const languageApi = await import('../plugins/quota-panel/server/language.mjs').catch(() => ({}));

test('a changed control card receives a new cache address while existing addresses keep their exact bundle', async t => {
  const directory=await mkdtemp(join(tmpdir(),'quota-ui-cache-'));t.after(()=>rm(directory,{recursive:true,force:true}));
  await mkdir(join(directory,'server'));await mkdir(join(directory,'ui'));
  await copyFile(new URL('../plugins/quota-panel/server/ui.mjs',import.meta.url),join(directory,'server/ui.mjs'));
  await writeFile(join(directory,'ui/control.html'),'<html lang="ru"><script>/* QUOTA_PANEL_BUNDLE */</script></html>');
  await writeFile(join(directory,'ui/control.mjs'),'// first card');
  const moduleUrl=pathToFileURL(join(directory,'server/ui.mjs')).href;
  const oldCard=await import(moduleUrl+'?revision=old');
  assert.equal(typeof oldCard.UI_URI,'string','card address must be derived from its actual bundle');
  assert.notEqual(oldCard.UI_URI,'ui://quota-panel/control-v1.html');
  const oldHtml=await oldCard.buildUi();
  await writeFile(join(directory,'ui/control.mjs'),'// translated card');
  assert.equal(await oldCard.buildUi(),oldHtml,'a cached address must never silently acquire different bytes');
  const newCard=await import(moduleUrl+'?revision=new');
  assert.notEqual(newCard.UI_URI,oldCard.UI_URI,'updated JavaScript must bypass an older cached card');
  assert.match(await newCard.buildUi({language:'en'}),/translated card/);
  assert.match(await newCard.buildUi({language:'en'}),/<html lang="en">/);
  await writeFile(join(directory,'ui/control.html'),'<html lang="ru"><h1>New layout</h1><script>/* QUOTA_PANEL_BUNDLE */</script></html>');
  const layoutCard=await import(moduleUrl+'?revision=layout');
  assert.notEqual(layoutCard.UI_URI,newCard.UI_URI,'updated markup must also invalidate an older cached card');
});

test('saved interface language is read without changing settings and tolerates an interrupted write', async t => {
  const directory=await mkdtemp(join(tmpdir(),'quota-language-'));t.after(()=>rm(directory,{recursive:true,force:true}));
  const settingsPath=join(directory,'settings.json');
  const getLanguage=languageApi.createLanguageReader({settingsPath});
  assert.equal(getLanguage(),'ru');
  const settings='\uFEFF'+JSON.stringify({language:'en',themeMode:'light',autoStartEnabled:false});
  await writeFile(settingsPath,settings);assert.equal(getLanguage(),'en');
  assert.equal(await readFile(settingsPath,'utf8'),settings);
  await writeFile(settingsPath,'{"language":');assert.equal(getLanguage(),'en');
  await writeFile(settingsPath,'{"language":"invalid"}');assert.equal(getLanguage(),'ru');
  await writeFile(settingsPath,'{"language":"ru"}');assert.equal(getLanguage(),'ru');
});

// Real subprocess fixture: a wrong command/argument or missing state transition fails the contract.
const fixture = `
const fs = require('node:fs');
const statePath = process.argv[1], operation = process.argv[2];
let running = fs.existsSync(statePath) && fs.readFileSync(statePath,'utf8') === 'running';
const before = running;
if (operation === '--start-json') running = true;
else if (operation === '--stop-json') running = false;
else if (operation !== '--status-json') process.exit(9);
fs.writeFileSync(statePath, running ? 'running' : 'stopped');
process.stdout.write(JSON.stringify({status:running?'running':'stopped',running,changed:running!==before,visible:false,paused:false,message:running?'Монитор работает.':'Монитор остановлен.',privateField:'secret'}));
`;

async function controllerFor(t, options = {}) {
  assert.equal(typeof controllerApi.createOverlayController, 'function', 'native lifecycle controller must exist');
  const directory = await mkdtemp(join(tmpdir(), 'quota panel тест '));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const statePath = join(directory, 'state.txt');
  const controller = controllerApi.createOverlayController({ command: process.execPath, prefixArgs: ['-e', fixture, '--', statePath], ...options });
  return { controller, statePath };
}

test('controller launches the exact native lifecycle commands and preserves idempotence', async t => {
  const { controller, statePath } = await controllerFor(t);
  assert.deepEqual(await controller.status(), { status: 'stopped', running: false, changed: false, visible: false, paused: false, message: 'Монитор остановлен.' });
  const first = await controller.start();
  assert.equal(first.running, true);
  assert.equal(first.changed, true);
  assert.equal(await readFile(statePath, 'utf8'), 'running');
  assert.equal((await controller.start()).changed, false);
  const stopped = await controller.stop();
  assert.equal(stopped.running, false);
  assert.equal(stopped.changed, true);
  assert.equal((await controller.stop()).changed, false);
  assert.equal((await controller.status()).running, false);
  assert(!JSON.stringify(first).includes('secret'));
});

test('controller contains spawn failures, nonzero exits, invalid JSON, invalid states and timeouts', async t => {
  assert.equal(typeof controllerApi.createOverlayController, 'function');
  const directory = await mkdtemp(join(tmpdir(), 'quota panel errors '));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const cases = [
    { command: join(directory, 'missing-secret.exe') },
    { command: process.execPath, prefixArgs: ['-e', "process.stderr.write('private-token');process.exit(7)", '--'] },
    { command: process.execPath, prefixArgs: ['-e', "process.stdout.write('private-token')", '--'] },
    { command: process.execPath, prefixArgs: ['-e', "process.stdout.write(JSON.stringify({status:'running',running:false}))", '--'] },
    { command: process.execPath, prefixArgs: ['-e', 'setTimeout(()=>{},10000)', '--'], timeoutMs: 70 },
  ];
  for (const settings of cases) {
    const controller = controllerApi.createOverlayController(settings);
    await assert.rejects(controller.start(), error => {
      assert.equal(typeof error.message, 'string');
      assert(!/private-token|missing-secret|ENOENT|Command failed/.test(error.message));
      assert.match(error.message, /монитор/i);
      return true;
    });
  }
});

test('MCP translates tool and resource labels and safe failures from the saved language', async () => {
  let language='en';
  const controller={status:async()=>{throw new Error('private-token');}};
  const handle=mcpApi.createMcpHandler({controller,readUi:async()=>'',getLanguage:()=>language});
  const list=async()=> (await handle({id:1,method:'tools/list'})).result.tools;
  assert.equal((await list())[1].title,'Start quota panel');
  assert.equal((await handle({id:2,method:'resources/list'})).result.resources[0].name,'Quota panel controls');
  const failure=await handle({id:3,method:'tools/call',params:{name:'show_quota_overlay'}});
  assert.match(failure.result.content[0].text,/Could not connect/);
  assert.doesNotMatch(failure.result.content[0].text,/private-token/);
  language='ru';assert.equal((await list())[1].title,'Запустить панель лимитов');
});

test('MCP discovers a global and thread control card and marks lifecycle mutations correctly', async t => {
  assert.equal(typeof mcpApi.createMcpHandler, 'function', 'quota-panel MCP handler must exist');
  const { controller } = await controllerFor(t);
  const handle = mcpApi.createMcpHandler({ controller, readUi: async () => '<main>Панель управления</main>' });
  const init = (await handle({ id: 1, method: 'initialize', params: { protocolVersion: '2025-06-18' } })).result;
  assert.equal(init.serverInfo.version, '1.2.0');
  assert.equal(init.serverInfo.title, 'Quota Panel');
  const tools = (await handle({ id: 2, method: 'tools/list' })).result.tools;
  assert.deepEqual(tools.map(tool => tool.name), ['show_quota_overlay', 'start_quota_overlay', 'stop_quota_overlay', 'get_quota_overlay_status','check_quota_panel_updates']);
  assert.deepEqual(tools[0]._meta['openai/ui'].entrypoints, [{ type: 'global' }, { type: 'thread' }]);
  assert.equal(tools[0].annotations.readOnlyHint, true);
  assert.equal(tools[3].annotations.readOnlyHint, true);
  assert.deepEqual(tools[3]._meta.ui.visibility, ['app']);
  for (const tool of tools.slice(1, 3)) {
    assert.equal(tool.annotations.readOnlyHint, false);
    assert.equal(tool.annotations.destructiveHint, false);
    assert.equal(tool.annotations.idempotentHint, true);
  }
  const resource = await handle({ id: 3, method: 'resources/read', params: { uri: mcpApi.UI_URI } });
  assert.equal(resource.result.contents[0].text, '<main>Панель управления</main>');
  assert.equal(resource.result.contents[0].mimeType, 'text/html;profile=mcp-app');
  const oldCard = await handle({ id: 4, method: 'resources/read', params: { uri: 'ui://quota-panel/control-v1.html' } });
  assert.equal(oldCard.result.contents[0].text, '<main>Панель управления</main>', 'older saved cards must still resolve to the current UI');
});

test('MCP routes lifecycle tools to the actual controller and rejects unknown requests', async t => {
  assert.equal(typeof mcpApi.createMcpHandler, 'function');
  const { controller, statePath } = await controllerFor(t);
  const handle = mcpApi.createMcpHandler({ controller, readUi: async () => '' });
  const call = (name, args = {}) => handle({ id: 5, method: 'tools/call', params: { name, arguments: args } });
  assert.equal((await call('show_quota_overlay')).result.structuredContent.running, false);
  assert.equal((await call('start_quota_overlay')).result.structuredContent.changed, true);
  assert.equal(await readFile(statePath, 'utf8'), 'running');
  assert.equal((await call('get_quota_overlay_status')).result.structuredContent.running, true);
  assert.equal((await call('stop_quota_overlay')).result.structuredContent.running, false);
  assert.equal((await call('start_quota_overlay', { command: 'other.exe' })).error.code, -32602);
  assert.equal((await call('start_quota_overlay', [])).error.code, -32602);
  assert.equal((await call('start_quota_overlay', null)).error.code, -32602);
  assert.equal((await call('show_quota_chips')).error.code, -32602);
  assert.equal((await handle({ id: 6, method: 'resources/read', params: { uri: 'file:///secret' } })).error.code, -32602);
  assert.equal((await handle({ id: 7, method: 'account/logout' })).error.code, -32601);
  assert.equal(await handle({ method: 'notifications/initialized' }), null);
});

test('stdio entrypoint serves MCP resources and closes after EOF without lifecycle mutations', async () => {
  const entrypoint = fileURLToPath(new URL('../plugins/quota-panel/server/index.mjs', import.meta.url));
  const child = spawn(process.execPath, [entrypoint], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
  let output = '', diagnostics = '';
  child.stdout.setEncoding('utf8'); child.stdout.on('data', value => { output += value; });
  child.stderr.setEncoding('utf8'); child.stderr.on('data', value => { diagnostics += value; });
  const closed = once(child, 'close');
  child.stdin.end([
    { jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: '2025-06-18' } },
    { jsonrpc: '2.0', method: 'notifications/initialized' },
    { jsonrpc: '2.0', id: 2, method: 'tools/list' },
    { jsonrpc: '2.0', id: 3, method: 'resources/read', params: { uri: mcpApi.UI_URI } },
  ].map(value => JSON.stringify(value)).join('\n') + '\n');
  const [code] = await closed;
  assert.equal(code, 0, diagnostics);
  assert.equal(diagnostics, '');
  const responses = output.trim().split('\n').map(line => JSON.parse(line));
  assert.equal(responses.length, 3);
  assert.equal(responses.find(value => value.id === 1).result.serverInfo.name, 'quota-panel');
  assert.equal(responses.find(value => value.id === 2).result.tools.length, 5);
  assert.match(responses.find(value => value.id === 3).result.contents[0].text, /<button id="start"/);
});

test('MCP returns a safe tool error when native lifecycle control fails', async () => {
  assert.equal(typeof mcpApi.createMcpHandler, 'function');
  const controller = { async status() { throw Error('private-account-token'); }, async start() { throw Error('private-account-token'); }, async stop() { throw Error('private-account-token'); } };
  const handle = mcpApi.createMcpHandler({ controller, readUi: async () => '' });
  for (const name of ['show_quota_overlay', 'start_quota_overlay', 'stop_quota_overlay', 'get_quota_overlay_status']) {
    const result = await handle({ id: 8, method: 'tools/call', params: { name, arguments: {} } });
    assert.equal(result.result.isError, true);
    assert(!JSON.stringify(result).includes('private-account-token'));
    assert.match(result.result.content[0].text, /монитор/i);
  }
});
