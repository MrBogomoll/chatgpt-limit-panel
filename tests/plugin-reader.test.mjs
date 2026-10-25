import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
const api = await import('../plugins/quota-chips/server/reader.mjs').catch(() => ({}));
const fixture = fileURLToPath(new URL('./fixtures/quota-app-server.cjs', import.meta.url));

test('reader initializes a subprocess and reads the actual JSON-RPC response', async () => {
  assert.equal(typeof api.RateLimitReader, 'function');
  const reader = new api.RateLimitReader({ command: process.execPath, args: [fixture], timeoutMs: 1000 });
  try { const raw = await reader.read(); assert.equal(raw.rateLimits.primary.usedPercent, 62); }
  finally { reader.close(); }
});

test('reader times out a stalled backend and releases its subprocess', async () => {
  assert.equal(typeof api.RateLimitReader, 'function');
  const reader = new api.RateLimitReader({ command: process.execPath, args: [fixture, '--hang'], timeoutMs: 100 });
  try { await assert.rejects(reader.read(), /timeout/i); }
  finally { reader.close(); }
});
