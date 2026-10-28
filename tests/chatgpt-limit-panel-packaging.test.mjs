import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { isAbsolute, join, resolve } from 'node:path';

const root = new URL('../plugins/chatgpt-limit-panel/', import.meta.url);
const json = async name => JSON.parse(await readFile(new URL(name, root), 'utf8'));

test('portable and compatibility manifests resolve the same local server without author profile paths', async () => {
  const portable = await json('plugin.json');
  const compatibility = await json('.codex-plugin/plugin.json');
  assert.equal(portable.name, 'quota-panel');
  assert.equal(portable.extensions['com.openai'].interface.displayName, 'ChatGPT Limit Panel');
  assert.equal(compatibility.interface.displayName, 'ChatGPT Limit Panel');
  assert.equal(portable.version, '1.4.0-beta.7');
  assert.equal(compatibility.version, '1.4.0-beta.7');
  assert.equal(portable.extensions['com.openai'].requires_local_executor, true);
  assert.equal(compatibility.requires_local_executor, true);
  assert.match(portable.extensions['com.openai'].interface.longDescription, /Windows x64/);
  assert.equal(compatibility.mcpServers, './.mcp.json');
  const portableMcp = await json('mcp.json');
  const compatibleMcp = await json(compatibility.mcpServers);
  assert.deepEqual(Object.keys(portableMcp.mcpServers), ['chatgpt-limit-panel']);
  assert.deepEqual(Object.keys(compatibleMcp.mcpServers), ['chatgpt-limit-panel']);
  const native = portableMcp.mcpServers['chatgpt-limit-panel'];
  const legacy = compatibleMcp.mcpServers['chatgpt-limit-panel'];
  assert.equal(native.type, 'stdio');
  const rootPath = fileURLToPath(root);
  const expand = value => value.replaceAll('${PLUGIN_ROOT}', rootPath);
  assert.equal(resolve(expand(native.command)), join(rootPath, 'runtime', 'node.exe'));
  assert.equal(resolve(expand(native.args[0])), fileURLToPath(new URL('server/index.mjs', root)));
  assert.equal(resolve(expand(native.cwd)), resolve(rootPath));
  assert.equal(resolve(rootPath, legacy.command), join(rootPath, 'runtime', 'node.exe'));
  assert.equal(resolve(rootPath, legacy.cwd, legacy.args[0]), fileURLToPath(new URL('server/index.mjs', root)));
  for (const config of [portableMcp, compatibleMcp]) {
    const text = JSON.stringify(config);
    assert(!/C:[/\\]|Users[/\\]|QUOTA_CODEX_BIN/.test(text));
  }
  assert.equal(isAbsolute(legacy.command), false);
});
