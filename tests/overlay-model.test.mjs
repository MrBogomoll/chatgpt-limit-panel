import { test } from 'node:test';
import assert from 'node:assert/strict';
import {normalizeQuota} from '../plugins/chatgpt-limit-panel/server/limits.mjs';
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

test('banked reset view formats nearest expiry and applies exact duration thresholds', () => {
  const day = 86400000;
  for (const [duration, tone, days] of [[10 * day + 1000, 'normal', 11], [10 * day, 'warning', 10], [3 * day + 1000, 'warning', 4], [3 * day, 'critical', 3], [1000, 'critical', 1], [-1000, 'critical', 0]]) {
    const expiresAt = (collectedAt + duration) / 1000;
    const view = api.overlayView({ ...snapshot, bankedResets: { availableCount: 5, expiresAt } }, { now: collectedAt, timeZone: 'Europe/Moscow' });
    assert.equal(view.resets.availableCount, 5);
    assert.equal(view.resets.daysRemaining, days);
    assert.equal(view.resets.tone, tone);
    assert.equal(view.resets.stale, false);
  }
  const view = api.overlayView({ ...snapshot, bankedResets: { availableCount: 5, expiresAt: Date.parse('2026-11-06T21:30:00Z') / 1000 } }, { now: collectedAt, timeZone: 'Europe/Moscow' });
  assert.equal(view.resets.date, '07.11.2026');
  assert.equal(view.resets.time, '00:30');
});

test('missing banked reset data stays unknown and stale data is explicit', () => {
  assert.equal(api.overlayView(null, { now: collectedAt }).resets.tone, 'unknown');
  const noDetails = api.overlayView({ ...snapshot, bankedResets: { availableCount: 5, expiresAt: null } }, { now: collectedAt });
  assert.deepEqual([noDetails.resets.availableCount, noDetails.resets.date, noDetails.resets.daysRemaining, noDetails.resets.tone], [5, '—', null, 'unknown']);
  const zero = api.overlayView({ ...snapshot, bankedResets: { availableCount: 0, expiresAt: collectedAt / 1000 + 1000 } }, { now: collectedAt });
  assert.deepEqual([zero.resets.availableCount, zero.resets.date, zero.resets.tone], [0, '—', 'normal']);
  const stale = api.overlayView({ ...snapshot, bankedResets: { availableCount: 2, expiresAt: collectedAt / 1000 + 86400 } }, { now: collectedAt + 181000 });
  assert.equal(stale.resets.stale, true);
  assert.equal(stale.resets.availableCount, 2);
});

test('reset list shows sorted individual expiry dates and reports incomplete server details',()=>{
  const seconds=collectedAt/1000,day=86400;
  const credit=expiresAt=>({status:'available',resetType:'codexRateLimits',expiresAt});
  const raw={rateLimitResetCredits:{availableCount:4,credits:[credit(null),credit(seconds+11*day),credit(seconds+2*day),credit(seconds-1)]}};
  const view=api.overlayView(normalizeQuota(raw,collectedAt),{now:collectedAt,timeZone:'UTC'});
  assert.equal(view.resets.availableCount,4);assert.equal(view.resets.detailsComplete,false);
  assert.deepEqual(view.resets.entries.map(x=>[x.expiresAt,x.tone,x.daysRemaining]),[[seconds+2*day,'critical',2],[seconds+11*day,'normal',11],[null,'unknown',null]]);
  assert.equal(view.resets.entries[0].date,'25.10.2026');assert.equal(view.resets.entries[0].time,'14:00');
  assert.equal(view.resets.entries[2].date,'—');assert.equal(view.resets.entries[2].time,'—');
  raw.rateLimitResetCredits.availableCount=3;
  assert.equal(api.overlayView(normalizeQuota(raw,collectedAt),{now:collectedAt}).resets.detailsComplete,true);
  raw.rateLimitResetCredits.credits=null;
  const missing=api.overlayView(normalizeQuota(raw,collectedAt),{now:collectedAt});
  assert.deepEqual(missing.resets.entries,[]);assert.equal(missing.resets.detailsComplete,false);
});

test('reset summary advances with the same unexpired entries shown in the submenu',()=>{
  const seconds=collectedAt/1000;
  const raw={rateLimitResetCredits:{availableCount:2,credits:[
    {status:'available',resetType:'codexRateLimits',expiresAt:seconds+1},
    {status:'available',resetType:'codexRateLimits',expiresAt:seconds+86400},
  ]}};
  const normalized=normalizeQuota(raw,collectedAt);
  const view=api.overlayView(normalized,{now:collectedAt+2000,timeZone:'UTC'});
  assert.equal(view.resets.availableCount,2,'count remains the authoritative server count until refreshed');
  assert.equal(view.resets.expiresAt,seconds+86400);
  assert.equal(view.resets.date,'24.10.2026');assert.equal(view.resets.time,'14:00');
  assert.equal(view.resets.expiresAt,view.resets.entries[0].expiresAt);
  assert.equal(view.resets.detailsComplete,false,'an expired detail cannot be counted as complete');
  const expired=api.overlayView(normalized,{now:collectedAt+86400001,timeZone:'UTC'});
  assert.deepEqual([expired.resets.expiresAt,expired.resets.date,expired.resets.time,expired.resets.tone],[null,'—','—','unknown']);
  assert.deepEqual(expired.resets.entries,[]);
  const unknown=api.overlayView({...normalized,bankedResets:{...normalized.bankedResets,entries:[{expiresAt:null}]}},{now:collectedAt});
  assert.equal(unknown.resets.expiresAt,null,'known details with no date cannot reuse a cached summary date');
  const missing=api.overlayView({...normalized,bankedResets:{...normalized.bankedResets,entries:null}},{now:collectedAt});
  assert.equal(missing.resets.expiresAt,seconds+1,'a trusted aggregate from an older snapshot remains usable without detailed rows');
  assert.equal(missing.resets.detailsComplete,false);
});

test('credit view distinguishes zero, negative, positive, unlimited and unavailable balances',()=>{
  for(const [balance,tone,display] of [['0','critical','0'],['-2.5','critical','-2.5'],['0.000001','normal','1e-6'],['-0.000001','critical','-1e-6'],['15.25','normal','15.3'],['1000','normal','1k'],['12345','normal','12.3k'],[null,'unknown','—']]) {
    const view=api.overlayView({...snapshot,credits:{balance,hasCredits:balance===null?false:true,unlimited:false}},{now:collectedAt});
    assert.equal(view.credits.balance,balance);assert.equal(view.credits.tone,tone);assert.equal(view.credits.display,display);assert.equal(view.credits.stale,false);
  }
  const unlimited=api.overlayView({...snapshot,credits:{balance:null,hasCredits:true,unlimited:true}},{now:collectedAt});
  assert.equal(unlimited.credits.display,'∞');assert.equal(unlimited.credits.tone,'normal');
  const stale=api.overlayView({...snapshot,credits:{balance:'100.123456789',hasCredits:true,unlimited:false}},{now:collectedAt+181000});
  assert.equal(stale.credits.balance,'100.123456789');assert.equal(stale.credits.stale,true);
  assert.equal(api.overlayView(null,{now:collectedAt}).credits.balance,null);
});
