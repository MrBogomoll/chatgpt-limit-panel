export const STALE_AFTER_MS = 180_000;

export function normalizeQuota(raw, collectedAt = Date.now()) {
  const hasMap = raw?.rateLimitsByLimitId != null;
  const legacy = raw?.rateLimits;
  const selected = hasMap ? raw.rateLimitsByLimitId.codex : legacy;
  const bucket = selected?.limitId == null || selected?.limitId === 'codex' ? selected : null;
  const candidates = [bucket?.primary, bucket?.secondary].filter(Boolean);
  const windows = ['short', 'weekly'].map(kind => {
    const value = candidates.find(x => kind === 'weekly' ? x.windowDurationMins === 10080 : Number.isFinite(x.windowDurationMins) && x.windowDurationMins > 0 && x.windowDurationMins < 10080);
    const valid = Number.isFinite(value?.usedPercent);
    const unlimited = kind === 'short' && !value && ['prolite','pro','promax'].includes(bucket?.planType)
      && Object.hasOwn(bucket,'primary') && Object.hasOwn(bucket,'secondary')
      && (bucket.primary === null || bucket.secondary === null)
      && candidates.some(window => window.windowDurationMins === 10080 && Number.isFinite(window.usedPercent));
    return {
      kind,
      availability: valid ? 'limited' : unlimited ? 'unlimited' : 'unknown',
      windowDurationMins: value?.windowDurationMins ?? (kind === 'weekly' ? 10080 : 300),
      remaining: valid ? Math.round(Math.max(0, Math.min(100, 100 - value.usedPercent))) : null,
      usedPercent: valid ? value.usedPercent : null,
      resetsAt: Number.isSafeInteger(value?.resetsAt) && value.resetsAt > 0 && Number.isFinite(new Date(value.resetsAt * 1000).getTime()) ? value.resetsAt : null,
    };
  });
  const known = windows.filter(x => x.availability !== 'unknown').length;
  const resetCredits = raw?.rateLimitResetCredits;
  const availableCount = Number.isSafeInteger(resetCredits?.availableCount) && resetCredits.availableCount >= 0 ? resetCredits.availableCount : null;
  const entries = Array.isArray(resetCredits?.credits) && availableCount !== null ? (availableCount === 0 ? [] : resetCredits.credits
    .filter(credit => credit?.status === 'available' && credit.resetType === 'codexRateLimits')
    .map(credit => ({ expiresAt: Number.isSafeInteger(credit.expiresAt) && credit.expiresAt > 0 && Number.isFinite(new Date(credit.expiresAt * 1000).getTime()) ? credit.expiresAt : null }))
    .filter(credit => credit.expiresAt === null || credit.expiresAt * 1000 > collectedAt)
    .sort((a,b) => a.expiresAt === null ? b.expiresAt === null ? 0 : 1 : b.expiresAt === null ? -1 : a.expiresAt-b.expiresAt)) : null;
  const bankedResets = { availableCount, expiresAt: entries?.find(credit => credit.expiresAt !== null)?.expiresAt ?? null, entries };
  const creditSnapshot=bucket?.credits;
  const credits={balance:typeof creditSnapshot?.balance==='string'&&/^-?\d+(?:\.\d+)?$/.test(creditSnapshot.balance)?creditSnapshot.balance:null,
    unlimited:creditSnapshot?.unlimited===true,hasCredits:typeof creditSnapshot?.hasCredits==='boolean'?creditSnapshot.hasCredits:null};
  const planType = ['free','go','plus','prolite','pro','promax','team','self_serve_business_prolite','self_serve_business_usage_based','business','ent26','enterprise_cbp_automation','enterprise_cbp_usage_based','enterprise','edu','edu_plus','edu_pro','sci','sci_plus'].includes(bucket?.planType) ? bucket.planType : null;
  return { status: known === 2 ? 'ok' : known === 1 ? 'partial' : 'unavailable', collectedAt, source: 'codex-app-server', planType, windows, bankedResets, credits };
}

export function contextForQuota(snapshot, { now = Date.now(), timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone } = {}) {
  const stale = snapshot?.stale === true || !Number.isFinite(snapshot?.collectedAt) || now - snapshot.collectedAt > STALE_AFTER_MS;
  const windows = snapshot?.windows ?? normalizeQuota(null).windows;
  const parts = (date, options) => new Intl.DateTimeFormat('ru-RU', { ...options, timeZone }).format(date);
  const dayKey = date => parts(date, { year: 'numeric', month: '2-digit', day: '2-digit' });
  return { content: windows.map(value => {
    const duration = value.windowDurationMins;
    const label = value.kind === 'weekly' ? 'Неделя' : duration % 60 === 0 ? `${duration / 60} ч` : `${duration} мин`;
    let title = `${label}: ${value.availability === 'unlimited' ? 'без ограничений' : value.remaining === null ? 'нет данных' : value.remaining + '%'}`;
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
