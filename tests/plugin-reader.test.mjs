import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
const api = await import('../plugins/chatgpt-limit-panel/server/reader.mjs').catch(() => ({}));
const fixture = fileURLToPath(new URL('./fixtures/limits-app-server.cjs', import.meta.url));

test('reader initializes a subprocess and reads the actual JSON-RPC response', async () => {
  assert.equal(typeof api.RateLimitReader, 'function');
  const reader = new api.RateLimitReader({ command: process.execPath, args: [fixture], timeoutMs: 1000 });
  try {
    const [raw, overlapping] = await Promise.all([reader.read(), reader.read()]);
    assert.equal(raw.rateLimits.primary.usedPercent, 62);
    assert(raw === overlapping, 'overlapping reads must share the same actual backend response');
  }
  finally { reader.close(); }
});

test('reader times out a stalled backend and releases its subprocess', async () => {
  assert.equal(typeof api.RateLimitReader, 'function');
  const reader = new api.RateLimitReader({ command: process.execPath, args: [fixture, '--hang'], timeoutMs: 100 });
  try { await assert.rejects(reader.read(), /timeout/i); }
  finally { reader.close(); }
});

for (const source of ['stdin', 'process']) {
  test(`late ${source} errors from a closed backend cannot close its replacement`, async () => {
    const reader = new api.RateLimitReader({ command: process.execPath, args: [fixture], timeoutMs: 1000 });
    try {
      assert.equal((await reader.read()).rateLimits.primary.usedPercent, 62);
      const oldChild = reader.child;
      reader.close();
      assert.equal((await reader.read()).rateLimits.primary.usedPercent, 62);
      const replacement = reader.child;
      assert.notEqual(replacement, oldChild);

      (source === 'stdin' ? oldChild.stdin : oldChild).emit('error', new Error('Late closed-backend error'));

      assert(reader.child === replacement, 'a late old-backend error must preserve the replacement');
      assert.equal(replacement.killed, false);
      assert.equal((await reader.read()).rateLimits.primary.usedPercent, 62);
      assert(reader.child === replacement, 'the replacement must serve the next quota request');
    } finally { reader.close(); }
  });
}

test('a rejected old initialization cannot close a replacement started before its catch runs', async () => {
  const reader = new api.RateLimitReader({ command: process.execPath, args: [fixture], timeoutMs: 1000 });
  try {
    const oldStart = reader.start();
    const oldRejection = assert.rejects(oldStart, /closed/i);
    const oldChild = reader.child;
    reader.close();
    const replacementStart = reader.start();
    const replacementResult = replacementStart.then(() => ({ started: true }), error => ({ error }));
    const replacement = reader.child;
    assert.notEqual(replacement, oldChild);

    await oldRejection;
    assert(reader.child === replacement, 'the old initialization catch must preserve the replacement');
    assert.equal((await replacementResult).started, true);
    assert.equal(replacement.killed, false);
    assert.equal((await reader.read()).rateLimits.primary.usedPercent, 62);
    assert(reader.child === replacement, 'the replacement must stay connected after initialization');
  } finally { reader.close(); }
});
