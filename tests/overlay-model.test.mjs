import { test } from 'node:test';
import assert from 'node:assert/strict';
const api = await import('../overlay/model.mjs').catch(() => ({}));
const collectedAt = Date.parse('2026-10-23T14:00:00Z');
const snapshot = { collectedAt, status: 'ok', windows: [{ kind: 'short', windowDurationMins: 300, remaining: 10, resetsAt: 1792774531 }, { kind: 'weekly', windowDurationMins: 10080, remaining: 30, resetsAt: 1793297434 }] };

test('overlay retains server resets and applies warning boundaries with readable colours', () => {
  assert.equal(typeof api.overlayView, 'function');
  const view = api.overlayView(snapshot, { now: collectedAt, timeZone: 'Europe/Moscow' });
  assert.deepEqual(view.rows.map(r => [r.period, r.value, r.date, r.time, r.tone]), [['5 ч', '10%', '23.10', '19:55', 'critical'], ['Нед.', '30%', '29.10', '21:10', 'warning']]);
  const neutral = api.overlayView({ ...snapshot, windows: snapshot.windows.map(w=>({...w,remaining:31})) }, {now:collectedAt});
  assert(neutral.rows.every(r=>r.tone==='normal'));
});

test('overlay labels short windows by their actual duration instead of assuming five hours', () => {
  for (const [windowDurationMins, period] of [[15, '15 мин'], [90, '90 мин'], [120, '2 ч'], [300, '5 ч'], [null, '—'], [undefined, '—'], [0, '—']]) {
    const view = api.overlayView({ ...snapshot, windows: [{ ...snapshot.windows[0], windowDurationMins }] }, { now: collectedAt });
    assert.equal(view.rows[0].period, period);
    assert.equal(view.rows[0].value, '10%');
    assert.equal(view.rows[1].period, 'Нед.');
  }
});

test('unknown and stale overlay states never masquerade as a fresh full quota', () => {
  assert.equal(typeof api.overlayView, 'function');
  const unknown = api.overlayView(null, { now:collectedAt });
  assert(unknown.rows.every(r=>r.value==='—'&&r.time==='—'&&r.tone==='unknown'));
  const old = api.overlayView(snapshot, {now: collectedAt+181000});
  assert.equal(old.stale,true);
  assert(old.rows.every(r=>r.marker==='устар.'));
  assert.equal(api.overlayView({...snapshot,stale:true},{now:collectedAt}).stale,true);
});

test('out-of-range server reset timestamps stay unknown without crashing the overlay', () => {
  const broken={...snapshot,windows:snapshot.windows.map(w=>({...w,resetsAt:Number.MAX_SAFE_INTEGER}))};
  const view=api.overlayView(broken,{now:collectedAt});
  assert(view.rows.every(r=>r.date==='—'&&r.time==='—'));
  assert.deepEqual(view.rows.map(r=>r.value),['10%','30%']);
});

test('progress carries numeric remaining quota and unknown stays null', () => {
  assert.deepEqual(api.overlayView(snapshot,{now:collectedAt}).rows.map(row=>row.remaining),[10,30]);
  assert(api.overlayView(null,{now:collectedAt}).rows.every(row=>row.remaining===null));
  const limits={...snapshot,windows:snapshot.windows.map((w,i)=>({...w,remaining:i===0?-4:150}))};
  assert.deepEqual(api.overlayView(limits,{now:collectedAt}).rows.map(row=>[row.remaining,row.value]),[[0,'0%'],[100,'100%']]);
});
