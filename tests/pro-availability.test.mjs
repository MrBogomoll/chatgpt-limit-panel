import {test} from 'node:test';
import assert from 'node:assert/strict';
import {normalizeQuota} from '../plugins/chatgpt-limit-panel/server/limits.mjs';
import {overlayView} from '../overlay/model.mjs';
import * as host from '../overlay/host.mjs';
const now=1794916800000;
const weekly={usedPercent:67,windowDurationMins:10080,resetsAt:1795521600};
const raw=(patch={})=>({rateLimits:{limitId:'codex',planType:'pro',primary:null,secondary:weekly,...patch}});

test('explicit absent Pro short window is unlimited, not unknown or a synthetic 100%',()=>{
 for(const planType of ['prolite','pro','promax'])assert.equal(normalizeQuota(raw({planType}),now).windows[0].availability,'unlimited');
 const snapshot=normalizeQuota(raw(),now),view=overlayView(snapshot,{now});
 assert.equal(snapshot.windows[0].availability,'unlimited');assert.equal(snapshot.status,'ok');
 assert.equal(view.rows[0].unlimited,true);assert.equal(view.rows[0].value,'∞');assert.equal(view.rows[0].remaining,null);
 assert.equal(view.rows[0].date,'—');assert.equal(view.rows[0].time,'—');assert.equal(view.rows[1].value,'33%');
});
test('missing, malformed, wrong-plan and foreign-bucket reads never imply unlimited',()=>{
 for(const value of [null,{},raw({planType:'plus'}),raw({primary:undefined}),raw({secondary:{}}),raw({limitId:'other'}),{rateLimitsByLimitId:{other:raw().rateLimits}}]){
  const snapshot=normalizeQuota(value,now);assert.notEqual(snapshot.windows[0].availability,'unlimited');
 }
 const snapshot=normalizeQuota(raw({primary:{usedPercent:20,windowDurationMins:300,resetsAt:null}}),now);
 assert.equal(snapshot.windows[0].availability,'limited');assert.equal(snapshot.windows[0].remaining,80);
});
test('transient incomplete reads preserve established shape; a plan change clears that assumption',()=>{
 assert.equal(typeof host.mergeQuotaSnapshot,'function');const previous=normalizeQuota(raw(),now);
 const merged=host.mergeQuotaSnapshot(previous,normalizeQuota({},now+1000));
 assert.equal(merged.windows[0].availability,'unlimited');assert.equal(merged.stale,true);
 const unknown=host.mergeQuotaSnapshot(previous,normalizeQuota(raw({planType:'unknown',primary:null,secondary:null}),now+1000));
 assert.equal(unknown.windows[0].availability,'unlimited');assert.equal(unknown.stale,true);
 const plus=host.mergeQuotaSnapshot(previous,normalizeQuota(raw({planType:'plus'}),now+1000));
 assert.notEqual(plus.windows[0].availability,'unlimited');
});
test('untrusted secret fields do not cross normalization or formatted view boundaries',()=>{
 const canary='TEST_ONLY_SECRET_DO_NOT_COPY';
 const snapshot=normalizeQuota({...raw(),access_token:canary,auth:{refresh_token:canary},rateLimits:{...raw().rateLimits,secret:canary,credits:{balance:canary}}},now);
 assert(!JSON.stringify(snapshot).includes(canary));assert(!JSON.stringify(overlayView(snapshot,{now})).includes(canary));
});
