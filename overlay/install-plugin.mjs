import { spawnSync } from 'node:child_process';
import { constants, copyFileSync, existsSync, lstatSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { homedir } from 'node:os';
import {createHash} from 'node:crypto';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const marketplaceName = 'quota-panel-local';
const pluginId = `quota-panel@${marketplaceName}`;
export const payloadFiles = [
  'mcp.json', '.codex-plugin/plugin.json', '.mcp.json',
  'assets/chatgpt-limit-panel.svg', 'assets/chatgpt-limit-panel.png', 'server/index.mjs', 'server/mcp.mjs', 'server/overlay-controller.mjs',
  'server/ui.mjs', 'server/language.mjs', 'ui/control.html', 'ui/control.mjs', 'overlay/host.mjs', 'overlay/model.mjs',
  'plugins/chatgpt-limit-panel/server/reader.mjs', 'plugins/chatgpt-limit-panel/server/limits.mjs',
  'dist/overlay/ChatGPTLimitPanel.exe', 'dist/overlay/ChatGPTLimitPanelLauncher.exe', 'dist/overlay/runtime.json',
  'runtime/node.exe', 'runtime/NODE-LICENSE.txt',
  'server/updates.mjs','plugins/chatgpt-limit-panel/server/updates.mjs',
  'server/uninstall.mjs','dist/overlay/ChatGPTLimitPanelUninstaller.exe',
];
export const updaterFiles=['overlay/install-plugin.mjs','overlay/updater.mjs'];
export const bundledDocuments=['README.txt', 'README.md', 'LICENSE.txt', 'LICENSE', 'UPGRADING.txt', 'UPGRADING.md', 'UNINSTALL.txt', 'UNINSTALL.md'];

/** Install a complete release through the native Codex plugin commands. */
export function installPlugin({
  stagePath,
  codexHome = process.env.CODEX_HOME || path.join(homedir(), '.codex'),
  codexBin = 'codex',
  codexPrefixArgs = [],
  stageOnly = false,
  expectedVersion,
} = {}) {
  if (!stagePath) throw new Error('A complete staged plugin package is required.');
  const stage = path.resolve(stagePath), home = path.resolve(codexHome);
  if (!existsSync(path.join(stage, 'README.txt')) && !existsSync(path.join(stage, 'README.md'))) throw new Error('Missing complete bundled plugin file: README.txt');
  const files = [...payloadFiles, ...[...updaterFiles,...bundledDocuments].filter(file => existsSync(path.join(stage, file)))];
  for (const relative of files) {
    let info;
    try { info = lstatSync(path.join(stage, relative)); } catch { /* Checked below. */ }
    if (!info?.isFile() || info.isSymbolicLink() || !info.size) throw new Error(`Missing complete bundled plugin file: ${relative}`);
  }
  const manifest = JSON.parse(readFileSync(path.join(stage, '.codex-plugin/plugin.json'), 'utf8'));
  if (manifest.name !== 'quota-panel' || !manifest.version || (expectedVersion && expectedVersion !== manifest.version) || manifest.interface?.displayName !== 'ChatGPT Limit Panel') {
    throw new Error('The compatibility plugin manifest must identify the ChatGPT Limit Panel release.');
  }

  const sources = path.join(home, 'plugins', 'local-sources');
  mkdirSync(sources, { recursive: true });
  const marketplaceRoot = mkdtempSync(path.join(sources, 'chatgpt-limit-panel-'));
  const sourcePath = path.join(marketplaceRoot, 'plugins', 'chatgpt-limit-panel');
  for (const relative of files) {
    const destination = path.join(sourcePath, relative);
    mkdirSync(path.dirname(destination), { recursive: true });
    copyFileSync(path.join(stage, relative), destination, constants.COPYFILE_EXCL);
  }
  const catalogPath = path.join(marketplaceRoot, '.agents', 'plugins', 'marketplace.json');
  mkdirSync(path.dirname(catalogPath), { recursive: true });
  writeFileSync(catalogPath, JSON.stringify({
    name: marketplaceName,
    interface: { displayName: 'ChatGPT Limit Panel' },
    plugins: [{ name: 'quota-panel', source: { source: 'local', path: './plugins/chatgpt-limit-panel' },
      policy: { installation: 'AVAILABLE', authentication: 'ON_INSTALL' }, category: 'Productivity' }],
  }, null, 2) + '\n', { flag: 'wx' });

  const configBackupPath = null;
  // Codex owns its configuration and sign-in. Do not duplicate a config file:
  // it can contain inline API keys even though this plugin never needs them.
  const inventoryFiles=[...files.map(relative=>'plugins/chatgpt-limit-panel/'+relative),'.agents/plugins/marketplace.json'];
  writeFileSync(path.join(marketplaceRoot,'uninstall-inventory.json'),JSON.stringify({schema:1,pluginId,files:Object.fromEntries(inventoryFiles.map(relative=>[relative,createHash('sha256').update(readFileSync(path.join(marketplaceRoot,relative))).digest('hex')]))},null,2)+'\n',{flag:'wx'});
  if(stageOnly)return {marketplaceRoot,sourcePath,version:manifest.version};
  const run = args => {
    const result = spawnSync(codexBin, [...codexPrefixArgs, ...args], {
      env: { ...process.env, CODEX_HOME: home }, encoding: 'utf8', windowsHide: true, timeout: 30_000,
    });
    if (result.error || result.status !== 0) throw new Error(`Native Codex plugin command failed (${args.slice(0, 3).join(' ')}).`);
    try { return JSON.parse(result.stdout); }
    catch { throw new Error('Native Codex plugin command returned invalid JSON.'); }
  };
  run(['plugin', 'marketplace', 'add', marketplaceRoot, '--json']);
  run(['plugin', 'add', pluginId, '--json']);
  const listed = run(['plugin', 'list', '--marketplace', marketplaceName, '--json']);
  const record = listed.installed?.find(plugin => plugin.pluginId === pluginId && plugin.installed && plugin.enabled && plugin.version === manifest.version);
  if (!record) throw new Error('Could not verify ChatGPT Limit Panel as installed and enabled in native Codex plugin metadata.');
  const installed={pluginId,name:manifest.name,marketplaceName,version:manifest.version,installed:true,enabled:true};
  return { installed, marketplaceRoot, sourcePath, configBackupPath };
}

if (process.argv[1] && pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url) {
  try { process.stdout.write(JSON.stringify(installPlugin({ stagePath: process.argv[2] }), null, 2) + '\n'); }
  catch (error) { process.stderr.write(`ChatGPT Limit Panel installation failed: ${error.message}\n`); process.exitCode = 1; }
}
