import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, readFile, readdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';

const api = await import('../overlay/package-plugin.mjs').catch(() => ({}));
const version = '1.3.1';
const allowedFiles = [
  '.codex-plugin/plugin.json', '.mcp.json', 'README.md', 'assets/chatgpt-limit-panel.svg', 'assets/chatgpt-limit-panel.png',
  'dist/overlay/ChatGPTLimitPanelLauncher.exe', 'dist/overlay/ChatGPTLimitPanel.exe', 'dist/overlay/runtime.json',
  'mcp.json', 'overlay/host.mjs', 'overlay/model.mjs',
  'plugins/chatgpt-limit-panel/server/limits.mjs', 'plugins/chatgpt-limit-panel/server/reader.mjs',
  'runtime/NODE-LICENSE.txt', 'runtime/node.exe',
  'server/index.mjs', 'server/mcp.mjs', 'server/overlay-controller.mjs', 'server/ui.mjs', 'server/language.mjs',
  'ui/control.html', 'ui/control.mjs',
  'server/updates.mjs','plugins/chatgpt-limit-panel/server/updates.mjs',
].sort();

async function fixtureBundle(t) {
  const directory = await mkdtemp(path.join(tmpdir(), 'quota beta package тест '));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const sourceRoot = path.join(directory, 'source');
  async function put(relative, content) {
    const filename = path.join(sourceRoot, relative);
    await mkdir(path.dirname(filename), { recursive: true });
    await writeFile(filename, content);
  }
  for (const relative of allowedFiles) {
    if (relative.startsWith('runtime/') || relative.endsWith('/runtime.json')) continue;
    const original = /^(overlay\/|plugins\/chatgpt-limit-panel\/server\/(?:updates|reader|limits)\.mjs|dist\/overlay\/)/.test(relative)
      ? relative : 'plugins/chatgpt-limit-panel/' + relative;
    await put(original, `fixture ${original}\n`);
  }
  const manifest = { name: 'quota-panel', version, mcpServers: './mcp.json', interface: { logo: './assets/chatgpt-limit-panel.svg' } };
  await put('plugins/chatgpt-limit-panel/plugin.json', JSON.stringify(manifest));
  await put('plugins/chatgpt-limit-panel/.codex-plugin/plugin.json', JSON.stringify({ ...manifest, mcpServers: './.mcp.json' }));
  await put('plugins/chatgpt-limit-panel/mcp.json', JSON.stringify({ mcpServers: { 'chatgpt-limit-panel': {
    command: '${PLUGIN_ROOT}/runtime/node.exe', args: ['${PLUGIN_ROOT}/server/index.mjs'], cwd: '${PLUGIN_ROOT}',
  } } }));
  await put('plugins/chatgpt-limit-panel/.mcp.json', JSON.stringify({ mcpServers: { 'chatgpt-limit-panel': {
    command: './runtime/node.exe', args: ['./server/index.mjs'], cwd: '.',
  } } }));
  await put('overlay/host.mjs', "import '../plugins/chatgpt-limit-panel/server/reader.mjs';\n");
  await put('dist/overlay/runtime.json', JSON.stringify({ node: 'C:/Users/author/node.exe', host: 'C:/Users/author/host.mjs' }));
  for (const relative of ['auth.json', 'config.toml', 'state.json', 'settings.json', 'panel.png',
    'old-client/app.asar', 'tests/private-fixture.json', 'plugins/chatgpt-limit-panel/tests/private-fixture.json',
    'plugins/chatgpt-limit-panel/assets/screenshot.png', '.git/config', 'dist/overlay/layout-test.exe']) {
    await put(relative, 'PRIVATE-DO-NOT-PACKAGE');
  }
  const nodePath = path.join(directory, 'fixture-node.exe');
  const nodeLicensePath = path.join(directory, 'fixture-node-LICENSE.txt');
  await writeFile(nodePath, 'MZ fixture bundled Node runtime');
  await writeFile(nodeLicensePath, 'Node.js fixture license text');
  return { sourceRoot, outputDir: path.join(directory, 'releases'), nodePath, nodeLicensePath, put };
}

async function filesUnder(directory, prefix = '') {
  const result = [];
  for (const entry of await readdir(path.join(directory, prefix), { withFileTypes: true })) {
    const relative = prefix ? prefix + '/' + entry.name : entry.name;
    if (entry.isDirectory()) result.push(...await filesUnder(directory, relative));
    else result.push(relative);
  }
  return result.sort();
}

test('beta staging copies only the uploadable plugin and its explicit runtime dependencies', async t => {
  assert.equal(typeof api.stagePluginBundle, 'function', 'the beta packager must expose isolated staging');
  const fixture = await fixtureBundle(t);
  const result = api.stagePluginBundle(fixture);
  assert.deepEqual(await filesUnder(result.stagePath), allowedFiles);
  assert.equal(await readFile(path.join(result.stagePath, 'runtime/node.exe'), 'utf8'), 'MZ fixture bundled Node runtime');
  assert.equal(await readFile(path.join(result.stagePath, 'runtime/NODE-LICENSE.txt'), 'utf8'), 'Node.js fixture license text');
  assert.equal(await readFile(path.join(result.stagePath, 'overlay/host.mjs'), 'utf8'), "import '../plugins/chatgpt-limit-panel/server/reader.mjs';\n");
});

test('beta staging replaces local runtime paths with relative bundled paths and automatic Codex discovery', async t => {
  assert.equal(typeof api.stagePluginBundle, 'function');
  const fixture = await fixtureBundle(t);
  const { stagePath } = api.stagePluginBundle(fixture);
  assert.deepEqual(JSON.parse(await readFile(path.join(stagePath, 'dist/overlay/runtime.json'), 'utf8')), {
    node: '../../runtime/node.exe', host: '../../overlay/host.mjs', codex: 'auto',
  });
  for (const relative of ['.codex-plugin/plugin.json', 'mcp.json', '.mcp.json', 'dist/overlay/runtime.json']) {
    const contents = await readFile(path.join(stagePath, relative), 'utf8');
    assert.doesNotMatch(contents, /[A-Z]:[\\/]|\/Users\/|\/home\//i, relative);
  }
});

test('beta staging uses a fresh versioned directory without overwriting an earlier stage', async t => {
  assert.equal(typeof api.stagePluginBundle, 'function');
  const fixture = await fixtureBundle(t);
  const first = api.stagePluginBundle(fixture);
  await writeFile(path.join(first.stagePath, 'review-marker.txt'), 'preserve earlier review');
  const second = api.stagePluginBundle(fixture);
  assert.notEqual(first.stagePath, second.stagePath);
  assert.match(path.basename(second.stagePath), /chatgpt-limit-panel-1\.3\.1/);
  assert.equal(path.dirname(second.stagePath), fixture.outputDir);
  assert.equal(await readFile(path.join(first.stagePath, 'review-marker.txt'), 'utf8'), 'preserve earlier review');
});

test('beta staging rejects a missing license before creating an incomplete package', async t => {
  assert.equal(typeof api.stagePluginBundle, 'function');
  const fixture = await fixtureBundle(t);
  await rm(fixture.nodeLicensePath);
  assert.throws(() => api.stagePluginBundle(fixture), /license/i);
});

test('beta staging rejects a source manifest with a mismatched version', async t => {
  assert.equal(typeof api.stagePluginBundle, 'function');
  const fixture = await fixtureBundle(t);
  await fixture.put('plugins/chatgpt-limit-panel/plugin.json', JSON.stringify({ name: 'quota-panel', version: '2.0.0' }));
  assert.throws(() => api.stagePluginBundle(fixture), /version|beta/i);
});

test('beta staging rejects source manifests that would leak author-specific absolute paths', async t => {
  assert.equal(typeof api.stagePluginBundle, 'function');
  const fixture = await fixtureBundle(t);
  await fixture.put('plugins/chatgpt-limit-panel/mcp.json', JSON.stringify({ mcpServers: { 'chatgpt-limit-panel': { command: 'C:/Users/author/node.exe' } } }));
  assert.throws(() => api.stagePluginBundle(fixture), /absolute|portable/i);
});

test('beta ZIP has upload manifests at its root, includes dotfiles, and publishes a matching SHA256', { skip: process.platform !== 'win32' }, async t => {
  assert.equal(typeof api.packagePlugin, 'function', 'the beta packager must produce a ZIP');
  const fixture = await fixtureBundle(t);
  const result = api.packagePlugin(fixture);
  assert.equal(path.basename(result.archivePath), 'chatgpt-limit-panel-1.3.1-windows-x64.zip');
  assert.equal(path.dirname(result.archivePath), fixture.outputDir);
  const zip = spawnSync('powershell.exe', ['-NoLogo', '-NoProfile', '-NonInteractive', '-Command',
    'Add-Type -AssemblyName System.IO.Compression.FileSystem; $archive = [System.IO.Compression.ZipFile]::OpenRead($env:QUOTA_TEST_ZIP); try { @($archive.Entries | ForEach-Object { $_.FullName }) | ConvertTo-Json -Compress } finally { $archive.Dispose() }'], {
    env: { ...process.env, QUOTA_TEST_ZIP: result.archivePath }, encoding: 'utf8', windowsHide: true, timeout: 10000,
  });
  assert.ifError(zip.error);
  assert.equal(zip.status, 0, zip.stderr);
  assert.deepEqual(JSON.parse(zip.stdout).map(name => name.replaceAll('\\', '/')).sort(), allowedFiles);
  const digest = createHash('sha256').update(await readFile(result.archivePath)).digest('hex');
  assert.equal(result.sha256, digest);
  assert.equal(await readFile(result.sha256Path, 'utf8'), `${digest}  chatgpt-limit-panel-1.3.1-windows-x64.zip\n`);
  assert.throws(() => api.packagePlugin(fixture), /already exists|overwrite/i);
});
