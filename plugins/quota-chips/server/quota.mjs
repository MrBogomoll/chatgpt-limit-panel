export const STALE_AFTER_MS = 180_000;

export function normalizeQuota(raw, collectedAt = Date.now()) {
  const hasMap = raw?.rateLimitsByLimitId != null;
  const legacy = raw?.rateLimits;
  const bucket = hasMap ? raw.rateLimitsByLimitId.codex : legacy?.limitId == null || legacy?.limitId === 'codex' ? legacy : null;
  const candidates = [bucket?.primary, bucket?.secondary].filter(Boolean);
  const windows = ['short', 'weekly'].map(kind => {
    const value = candidates.find(x => kind === 'weekly' ? x.windowDurationMins === 10080 : Number.isFinite(x.windowDurationMins) && x.windowDurationMins > 0 && x.windowDurationMins < 10080);
    const valid = Number.isFinite(value?.usedPercent);
    return {
      kind,
      windowDurationMins: value?.windowDurationMins ?? (kind === 'weekly' ? 10080 : 300),
      remaining: valid ? Math.round(Math.max(0, Math.min(100, 100 - value.usedPercent))) : null,
      usedPercent: valid ? value.usedPercent : null,
      resetsAt: Number.isSafeInteger(value?.resetsAt) && value.resetsAt > 0 ? value.resetsAt : null,
    };
  });
  const known = windows.filter(x => x.remaining !== null).length;
  return { status: known === 2 ? 'ok' : known === 1 ? 'partial' : 'unavailable', collectedAt, source: 'codex-app-server', windows };
}

export function contextForQuota(snapshot, { now = Date.now(), timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone } = {}) {
  const stale = snapshot?.stale === true || !Number.isFinite(snapshot?.collectedAt) || now - snapshot.collectedAt > STALE_AFTER_MS;
  const windows = snapshot?.windows ?? normalizeQuota(null).windows;
  const parts = (date, options) => new Intl.DateTimeFormat('ru-RU', { ...options, timeZone }).format(date);
  const dayKey = date => parts(date, { year: 'numeric', month: '2-digit', day: '2-digit' });
  return { content: windows.map(value => {
    const duration = value.windowDurationMins;
    const label = value.kind === 'weekly' ? 'Неделя' : duration % 60 === 0 ? `${duration / 60} ч` : `${duration} мин`;
    let title = `${label}: ${value.remaining === null ? 'нет данных' : value.remaining + '%'}`;
    if (value.remaining !== null && value.resetsAt !== null) {
      const reset = new Date(value.resetsAt * 1000);
      const time = parts(reset, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
      const sameDay = dayKey(reset) === dayKey(new Date(now));
      title += sameDay ? ` · сброс ${time}` : ` · ${parts(reset, { day: '2-digit', month: '2-digit' })}, ${time}`;
    } else if (value.remaining !== null) title += ' · сброс —';
    if (stale && value.remaining !== null) title += ' · устар.';
    return { type: 'text', text: title, annotations: { audience: ['user'] }, _meta: { 'openai/group': { title }, 'openai/title': label } };
  }) };
}
