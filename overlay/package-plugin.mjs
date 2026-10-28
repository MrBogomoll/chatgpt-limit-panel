import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { constants, copyFileSync, existsSync, lstatSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const projectRoot = fileURLToPath(new URL('../', import.meta.url));
const version = '1.3.0';
const archiveName = `chatgpt-limit-panel-${version}-windows-x64.zip`;
const pluginFiles = [
  // The installed desktop parser ignores bundled MCP servers when root
  // plugin.json exists. Ship its supported compatibility entry point only.
  'mcp.json', '.codex-plugin/plugin.json', '.mcp.json', 'README.md',
  'assets/chatgpt-limit-panel.svg', 'assets/chatgpt-limit-panel.png', 'server/index.mjs', 'server/mcp.mjs', 'server/overlay-controller.mjs',
  'server/ui.mjs', 'server/language.mjs', 'server/updates.mjs', 'ui/control.html', 'ui/control.mjs',
];
const dependencyFiles = [
  'overlay/host.mjs', 'overlay/model.mjs',
  'plugins/chatgpt-limit-panel/server/updates.mjs',
  'plugins/chatgpt-limit-panel/server/reader.mjs', 'plugins/chatgpt-limit-panel/server/limits.mjs',
  'dist/overlay/ChatGPTLimitPanel.exe', 'dist/overlay/ChatGPTLimitPanelLauncher.exe',
];

function requireFile(filename, description) {
  let info;
  try { info = lstatSync(filename); }
  catch { throw new Error(`Missing ${description}: ${filename}`); }
  if (!info.isFile() || info.isSymbolicLink() || info.size === 0) throw new Error(`Invalid ${description}: ${filename}`);
}

function requirePortableJson(value, filename) {
  if (typeof value === 'string' && (path.win32.isAbsolute(value) || path.posix.isAbsolute(value))) {
    throw new Error(`Plugin manifest must use portable paths, not absolute paths: ${filename}`);
  }
  if (value && typeof value === 'object') {
    for (const item of Object.values(value)) requirePortableJson(item, filename);
  }
}

/** Stage only the reviewed release payload. Each invocation preserves every earlier stage. */
export function stagePluginBundle({
  sourceRoot = projectRoot,
  outputDir = path.join(projectRoot, 'dist', 'releases'),
  nodePath = process.execPath,
  nodeLicensePath = path.join(projectRoot, 'dist', 'third-party', 'node-LICENSE.txt'),
} = {}) {
  const root = path.resolve(sourceRoot);
  const output = path.resolve(outputDir);
  const files = [
    ...pluginFiles.map(relative => ({ source: path.join(root, 'plugins', 'chatgpt-limit-panel', relative), destination: relative })),
    ...dependencyFiles.map(relative => ({ source: path.join(root, relative), destination: relative })),
    { source: path.resolve(nodePath), destination: 'runtime/node.exe' },
    { source: path.resolve(nodeLicensePath), destination: 'runtime/NODE-LICENSE.txt' },
  ];
  for (const file of files) requireFile(file.source, file.destination);
  for (const relative of ['plugin.json', '.codex-plugin/plugin.json', 'mcp.json', '.mcp.json']) {
    const filename = path.join(root, 'plugins', 'chatgpt-limit-panel', relative);
    const manifest = JSON.parse(readFileSync(filename, 'utf8').replace(/^\uFEFF/, ''));
    requirePortableJson(manifest, relative);
    if (relative.endsWith('plugin.json') && (manifest.name !== 'quota-panel' || manifest.version !== version)) {
      throw new Error(`Plugin manifest must identify quota-panel release version ${version}: ${relative}`);
    }
  }
  mkdirSync(output, { recursive: true });
  const stagePath = mkdtempSync(path.join(output, `chatgpt-limit-panel-${version}-stage-`));
  for (const file of files) {
    const target = path.join(stagePath, file.destination);
    mkdirSync(path.dirname(target), { recursive: true });
    copyFileSync(file.source, target, constants.COPYFILE_EXCL);
  }
  writeFileSync(path.join(stagePath, 'dist', 'overlay', 'runtime.json'), JSON.stringify({
    node: '../../runtime/node.exe', host: '../../overlay/host.mjs', codex: 'auto',
  }, null, 2) + '\n', { flag: 'wx' });
  return { version, stagePath };
}

/** Produce the local Windows release archive and checksum; this never installs or publishes it. */
export function packagePlugin(options = {}) {
  const outputDir = path.resolve(options.outputDir || path.join(projectRoot, 'dist', 'releases'));
  const archivePath = path.join(outputDir, archiveName);
  const sha256Path = archivePath + '.sha256';
  if (existsSync(archivePath) || existsSync(sha256Path)) {
    throw new Error('Release already exists; choose another output directory to preserve the earlier review.');
  }
  const staged = stagePluginBundle({ ...options, outputDir });
  const archive = spawnSync('powershell.exe', ['-NoLogo', '-NoProfile', '-NonInteractive', '-Command',
    "$ErrorActionPreference = 'Stop'; Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::CreateFromDirectory($env:QUOTA_PACKAGE_STAGE, $env:QUOTA_PACKAGE_ARCHIVE, [System.IO.Compression.CompressionLevel]::Optimal, $false)"], {
    env: { ...process.env, QUOTA_PACKAGE_STAGE: staged.stagePath, QUOTA_PACKAGE_ARCHIVE: archivePath },
    encoding: 'utf8', windowsHide: true, timeout: 120000,
  });
  if (archive.error || archive.status !== 0) {
    throw new Error(`Could not create beta ZIP: ${archive.error?.message || archive.stderr.trim() || `exit ${archive.status}`}`);
  }
  const sha256 = createHash('sha256').update(readFileSync(archivePath)).digest('hex');
  writeFileSync(sha256Path, `${sha256}  ${archiveName}\n`, { flag: 'wx' });
  return { ...staged, archivePath, sha256Path, sha256 };
}

if (process.argv[1] && pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url) {
  try { process.stdout.write(JSON.stringify(packagePlugin(), null, 2) + '\n'); }
  catch (error) { process.stderr.write(`Release packaging failed: ${error.message}\n`); process.exitCode = 1; }
}
