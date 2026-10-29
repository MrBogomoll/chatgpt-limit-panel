import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, readFile, readdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';

const api = await import('../overlay/install-plugin.mjs').catch(() => ({}));
const payloadFiles = [
  'mcp.json', '.codex-plugin/plugin.json', '.mcp.json', 'README.md',
  'assets/chatgpt-limit-panel.svg', 'assets/chatgpt-limit-panel.png', 'server/index.mjs', 'server/mcp.mjs', 'server/overlay-controller.mjs',
  'server/ui.mjs', 'server/language.mjs', 'ui/control.html', 'ui/control.mjs', 'overlay/host.mjs', 'overlay/model.mjs',
  'plugins/chatgpt-limit-panel/server/reader.mjs', 'plugins/chatgpt-limit-panel/server/limits.mjs',
  'dist/overlay/ChatGPTLimitPanel.exe', 'dist/overlay/ChatGPTLimitPanelLauncher.exe', 'dist/overlay/runtime.json',
  'runtime/node.exe', 'runtime/NODE-LICENSE.txt',
  'server/updates.mjs','plugins/chatgpt-limit-panel/server/updates.mjs',
];

async function fixture(t, installed = true) {
  const directory = await mkdtemp(path.join(tmpdir(), 'quota install тест '));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const stagePath = path.join(directory, 'stage'), codexHome = path.join(directory, '.codex');
  for (const filename of payloadFiles) {
    await mkdir(path.dirname(path.join(stagePath, filename)), { recursive: true });
    await writeFile(path.join(stagePath, filename), 'payload:' + filename);
  }
  for (const filename of ['.codex-plugin/plugin.json']) {
    await writeFile(path.join(stagePath, filename), JSON.stringify({ name: 'quota-panel', version: '1.4.3-beta.1',
      interface: { displayName: 'ChatGPT Limit Panel' }, extensions: { 'com.openai': { interface: { displayName: 'ChatGPT Limit Panel' } } } }));
  }
  await writeFile(path.join(stagePath, 'private-author-config.toml'), 'do not install');
  await mkdir(codexHome, { recursive: true });
  const originalConfig = '# preserve locale and other plugins\nlocale = "ru"\n[plugins."quota-chips@quota-chips-local"]\nenabled = true\n';
  await writeFile(path.join(codexHome, 'config.toml'), originalConfig);
  const log = path.join(directory, 'calls.jsonl'), cli = path.join(directory, 'fixture-cli.cjs');
  await writeFile(cli, `
const fs = require('node:fs'), path = require('node:path');
const log = process.argv[2], args = process.argv.slice(3);
fs.appendFileSync(log, JSON.stringify({args,home:process.env.CODEX_HOME})+'\\n');
if(args[0] !== 'plugin') process.exit(11);
if(args[1] === 'marketplace') {
  if(args[2] !== 'add' || args[4] !== '--json') process.exit(12);
  const catalog = JSON.parse(fs.readFileSync(path.join(args[3],'.agents/plugins/marketplace.json'),'utf8'));
  if(catalog.name !== 'quota-panel-local' || catalog.plugins[0].name !== 'quota-panel' || catalog.plugins[0].source.path !== './plugins/chatgpt-limit-panel') process.exit(13);
  if(!fs.existsSync(path.join(args[3],'plugins/chatgpt-limit-panel/runtime/node.exe'))) process.exit(14);
  console.log(JSON.stringify({name:catalog.name}));
} else if(args[1] === 'add') {
  if(args[2] !== 'quota-panel@quota-panel-local' || args[3] !== '--json') process.exit(15);
  console.log(JSON.stringify({installed:true}));
} else if(args[1] === 'list') {
  if(JSON.stringify(args.slice(2)) !== JSON.stringify(['--marketplace','quota-panel-local','--json'])) process.exit(16);
  console.log(JSON.stringify({installed:${installed ? "[{pluginId:'quota-panel@quota-panel-local',name:'quota-panel',marketplaceName:'quota-panel-local',installed:true,enabled:true,version:'1.4.3-beta.1'}]" : '[]'}}));
} else process.exit(17);
`);
  return { stagePath, codexHome, codexBin: process.execPath, codexPrefixArgs: [cli, log], log, originalConfig };
}

test('plugin installer registers a complete private marketplace and verifies native installed state', async t => {
  assert.equal(typeof api.installPlugin, 'function', 'native plugin installer must exist');
  const settings = await fixture(t);
  const result = api.installPlugin(settings);
  assert.equal(result.installed.pluginId, 'quota-panel@quota-panel-local');
  assert.equal(result.installed.enabled, true);
  assert.equal(await readFile(path.join(result.sourcePath, 'runtime/node.exe'), 'utf8'), 'payload:runtime/node.exe');
  assert.equal(await readFile(path.join(result.sourcePath, 'dist/overlay/ChatGPTLimitPanelLauncher.exe'), 'utf8'), 'payload:dist/overlay/ChatGPTLimitPanelLauncher.exe');
  assert.equal(await readFile(path.join(result.sourcePath, '.codex-plugin/plugin.json'), 'utf8'), await readFile(path.join(settings.stagePath, '.codex-plugin/plugin.json'), 'utf8'));
  assert.equal((await readdir(result.sourcePath)).includes('private-author-config.toml'), false);
  assert.equal((await readdir(result.sourcePath)).includes('plugin.json'), false);
  assert.equal(await readFile(result.configBackupPath, 'utf8'), settings.originalConfig);
  assert.equal(await readFile(path.join(settings.codexHome, 'config.toml'), 'utf8'), settings.originalConfig);
  const calls = (await readFile(settings.log, 'utf8')).trim().split('\n').map(JSON.parse);
  assert.equal(calls.length, 3);
  assert(calls.every(call => call.home === settings.codexHome));
  assert.deepEqual(calls[1].args, ['plugin', 'add', 'quota-panel@quota-panel-local', '--json']);
  assert.deepEqual(calls[2].args, ['plugin', 'list', '--marketplace', 'quota-panel-local', '--json']);
});

test('plugin installer refuses incomplete source folders before writing or invoking Codex', async t => {
  assert.equal(typeof api.installPlugin, 'function');
  const settings = await fixture(t);
  await rm(path.join(settings.stagePath, 'runtime/node.exe'));
  assert.throws(() => api.installPlugin(settings), /runtime\/node\.exe|complete|bundled/i);
  assert.deepEqual(await readdir(settings.codexHome), ['config.toml']);
});

test('plugin installer does not report success from an add response without native installed verification', async t => {
  assert.equal(typeof api.installPlugin, 'function');
  const settings = await fixture(t, false);
  assert.throws(() => api.installPlugin(settings), /installed|verify|verification/i);
});
