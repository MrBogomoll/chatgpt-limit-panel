import { normalizeQuota, STALE_AFTER_MS } from '../plugins/quota-chips/server/quota.mjs';

export function overlayView(snapshot, { now = Date.now(), timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone } = {}) {
  snapshot ??= normalizeQuota(null, now);
  const stale = snapshot.stale === true || !Number.isFinite(snapshot.collectedAt) || now - snapshot.collectedAt > STALE_AFTER_MS;
  const date = new Intl.DateTimeFormat('ru-RU', { day: '2-digit', month: '2-digit', timeZone });
  const time = new Intl.DateTimeFormat('ru-RU', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZone });
  return { stale, collectedAt: snapshot.collectedAt, rows: ['short', 'weekly'].map(kind => {
    const w = snapshot.windows?.find(w => w.kind === kind);
    const known = Number.isFinite(w?.remaining);
    const remaining = known ? Math.min(100, Math.max(0, w.remaining)) : null;
    const candidate = Number.isSafeInteger(w?.resetsAt) && w.resetsAt > 0 ? new Date(w.resetsAt * 1000) : null;
    const reset = candidate && Number.isFinite(candidate.getTime()) ? candidate : null;
    return { period: kind === 'weekly' ? 'Нед.' : w?.windowDurationMins % 60 === 0 ? `${w.windowDurationMins / 60} ч` : '5 ч', value: known ? `${remaining}%` : '—', remaining,
      date: reset ? date.format(reset) : '—', time: reset ? time.format(reset) : '—', marker: known && stale ? 'устар.' : '',
      tone: !known ? 'unknown' : remaining <= 10 ? 'critical' : remaining <= 30 ? 'warning' : 'normal' };
  }) };
}
