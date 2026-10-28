import { test } from 'node:test';
import assert from 'node:assert/strict';
const api = await import('../plugins/chatgpt-limit-panel/server/quota.mjs').catch(() => ({}));
const sample = { rateLimitsByLimitId: { codex: { limitId: 'codex', primary: { usedPercent: 62, windowDurationMins: 300, resetsAt: 1792774530 }, secondary: { usedPercent: 57, windowDurationMins: 10080, resetsAt: 1793297434 } }, codex_other: { primary: { usedPercent: 1, windowDurationMins: 300, resetsAt: 100 } } } };

test('uses the Codex bucket, returns server resets, and calculates remaining quota', () => {
  assert.equal(typeof api.normalizeQuota, 'function');
  const result = api.normalizeQuota(sample, 1792760000000);
  assert.equal(result.status, 'ok');
  assert.deepEqual(result.windows.map(x => [x.kind, x.remaining, x.resetsAt]), [['short', 38, 1792774530], ['weekly', 43, 1793297434]]);
});

test('missing and unrelated quota buckets never turn into 100 percent', () => {
  assert.equal(typeof api.normalizeQuota, 'function');
  for (const raw of [null, {}, { rateLimits: { limitId: 'other', primary: { usedPercent: 1 } } }, { rateLimitsByLimitId: { other: sample.rateLimitsByLimitId.codex } }]) {
    const result = api.normalizeQuota(raw);
    assert.equal(result.status, 'unavailable');
    assert(result.windows.every(x => x.remaining === null));
  }
});

test('zero, reversed windows, invalid percentages, and partial reset data are handled', () => {
  assert.equal(typeof api.normalizeQuota, 'function');
  const result = api.normalizeQuota({ rateLimits: { limitId: 'codex', secondary: { usedPercent: 100, windowDurationMins: 300, resetsAt: 123 }, primary: { usedPercent: null, windowDurationMins: 10080 } } });
  assert.equal(result.windows[0].remaining, 0);
  assert.equal(result.windows[1].remaining, null);
  assert.equal(result.windows[1].resetsAt, null);
});

test('reset timestamps outside the JavaScript date range remain unknown while preserving quota', () => {
  for (const resetsAt of [8_640_000_000_001, Number.MAX_SAFE_INTEGER]) {
    const snapshot = api.normalizeQuota({ rateLimits: { primary: { usedPercent: 25, windowDurationMins: 300, resetsAt } } }, 1792760000000);
    assert.equal(snapshot.windows[0].resetsAt, null);
    assert.equal(snapshot.windows[0].remaining, 75);
    const context = api.contextForQuota(snapshot, { now: 1792760000000, timeZone: 'Europe/Moscow' });
    assert.equal(context.content[0].text, '5 ч: 75% · сброс —');
  }
  const boundary = api.normalizeQuota({ rateLimits: { primary: { usedPercent: 25, windowDurationMins: 300, resetsAt: 8_640_000_000_000 } } });
  assert.equal(boundary.windows[0].resetsAt, 8_640_000_000_000);
});

test('publishes exactly two user-visible group labels with local reset times', () => {
  assert.equal(typeof api.contextForQuota, 'function');
  const context = api.contextForQuota(api.normalizeQuota(sample, 1792760000000), { now: 1792760000000, timeZone: 'Europe/Moscow' });
  assert.equal(context.content.length, 2);
  assert.equal(context.content[0]._meta['openai/group'].title, '5 ч: 38% · сброс 19:55');
  assert.equal(context.content[1]._meta['openai/group'].title, 'Неделя: 43% · 29.10, 21:10');
  assert.deepEqual(context.content[0].annotations.audience, ['user']);
});

test('stale quota labels are explicit and never imply an automatic reset', () => {
  assert.equal(typeof api.contextForQuota, 'function');
  const snapshot = api.normalizeQuota(sample, 1792760000000);
  const context = api.contextForQuota(snapshot, { now: 1792760300000, timeZone: 'Europe/Moscow' });
  assert(context.content.every(x => x._meta['openai/group'].title.includes('устар.')));
  assert.equal(snapshot.windows[0].remaining, 38);
});
