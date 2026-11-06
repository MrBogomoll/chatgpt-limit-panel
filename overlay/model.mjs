import { normalizeQuota, STALE_AFTER_MS } from '../plugins/chatgpt-limit-panel/server/limits.mjs';

function creditDisplay(balance) {
  if(balance===null)return '—';
  const numeric=Number(balance);
  if(!/^-?0+(?:\.0+)?$/.test(balance)&&Math.abs(numeric)<0.1) {
    const fraction=balance.split('.')[1]??'',first=fraction.search(/[1-9]/);
    return (balance.startsWith('-')?'-':'')+fraction[first]+(fraction[first+1]&&fraction[first+1]!=='0'?'.'+fraction[first+1]:'')+'e-'+(first+1);
  }
  if(Number.isFinite(numeric)&&Math.abs(numeric)<1e15)return new Intl.NumberFormat('en',{notation:'compact',maximumFractionDigits:1}).format(numeric).toLowerCase();
  const whole=balance.replace(/^-?/,'').split('.')[0].replace(/^0+/,'');
  return (balance.startsWith('-')?'-':'')+whole[0]+(whole[1]&&whole[1]!=='0'?'.'+whole[1]:'')+'e'+(whole.length-1);
}

export function overlayView(snapshot, { now = Date.now(), timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone } = {}) {
  snapshot ??= normalizeQuota(null, now);
  const stale = snapshot.stale === true || !Number.isFinite(snapshot.collectedAt) || now - snapshot.collectedAt > STALE_AFTER_MS;
  const date = new Intl.DateTimeFormat('ru-RU', { day: '2-digit', month: '2-digit', timeZone });
  const time = new Intl.DateTimeFormat('ru-RU', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZone });
  const availableCount = Number.isSafeInteger(snapshot.bankedResets?.availableCount) && snapshot.bankedResets.availableCount >= 0 ? snapshot.bankedResets.availableCount : null;
  const fullDate = new Intl.DateTimeFormat('ru-RU', { day:'2-digit',month:'2-digit',year:'numeric',timeZone });
  const resetEntries = availableCount !== null && Array.isArray(snapshot.bankedResets?.entries) ? snapshot.bankedResets.entries
    .filter(entry=>entry&&entry.expiresAt===null || entry&&Number.isSafeInteger(entry.expiresAt)&&entry.expiresAt>0&&entry.expiresAt*1000>now&&Number.isFinite(new Date(entry.expiresAt*1000).getTime()))
    .map(entry=>{
      const expiration=entry.expiresAt===null?null:new Date(entry.expiresAt*1000),remaining=expiration?expiration.getTime()-now:null;
      return {expiresAt:entry.expiresAt,date:expiration?fullDate.format(expiration):'—',time:expiration?time.format(expiration):'—',
        daysRemaining:expiration?Math.max(0,Math.ceil(remaining/86400000)):null,tone:!expiration?'unknown':remaining<=3*86400000?'critical':remaining<=10*86400000?'warning':'normal'};
    }).sort((a,b)=>a.expiresAt===null?b.expiresAt===null?0:1:b.expiresAt===null?-1:a.expiresAt-b.expiresAt) : [];
  // Summary and submenu must advance together when a known credit expires
  // between account reads. A cached aggregate is only for older snapshots
  // whose detailed rows were unavailable, not for a known empty detail list.
  const expirySeconds = Array.isArray(snapshot.bankedResets?.entries)
    ? resetEntries.find(entry=>entry.expiresAt!==null)?.expiresAt ?? null
    : snapshot.bankedResets?.expiresAt;
  const candidateExpiry = availableCount > 0 && Number.isSafeInteger(expirySeconds) && expirySeconds > 0 ? new Date(expirySeconds * 1000) : null;
  const expiry = candidateExpiry && Number.isFinite(candidateExpiry.getTime()) ? candidateExpiry : null;
  const remainingMs = expiry ? expiry.getTime() - now : null;
  const resets = {
    availableCount, expiresAt: expiry ? expirySeconds : null,
    date: expiry ? new Intl.DateTimeFormat('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone }).format(expiry) : '—',
    time: expiry ? time.format(expiry) : '—', daysRemaining: expiry ? Math.max(0, Math.ceil(remainingMs / 86400000)) : null,
    tone: availableCount === 0 ? 'normal' : !expiry ? 'unknown' : remainingMs <= 3 * 86400000 ? 'critical' : remainingMs <= 10 * 86400000 ? 'warning' : 'normal', stale,
    entries:resetEntries,detailsComplete:availableCount!==null&&Array.isArray(snapshot.bankedResets?.entries)&&resetEntries.length===availableCount,
  };
  const creditSnapshot=snapshot.credits;
  const balance=typeof creditSnapshot?.balance==='string'&&/^-?\d+(?:\.\d+)?$/.test(creditSnapshot.balance)?creditSnapshot.balance:null;
  const unlimited=creditSnapshot?.unlimited===true;
  const credits={balance,display:unlimited?'∞':creditDisplay(balance),unlimited,hasCredits:typeof creditSnapshot?.hasCredits==='boolean'?creditSnapshot.hasCredits:null,
    tone:unlimited?'normal':balance===null?'unknown':balance.startsWith('-')||/^0+(?:\.0+)?$/.test(balance)?'critical':'normal',stale};
  return { stale, collectedAt: snapshot.collectedAt, resets, credits, rows: ['short', 'weekly'].map(kind => {
    const w = snapshot.windows?.find(w => w.kind === kind);
    const known = Number.isFinite(w?.remaining);
    const remaining = known ? Math.min(100, Math.max(0, w.remaining)) : null;
    const candidate = Number.isSafeInteger(w?.resetsAt) && w.resetsAt > 0 ? new Date(w.resetsAt * 1000) : null;
    const reset = candidate && Number.isFinite(candidate.getTime()) ? candidate : null;
    const duration = w?.windowDurationMins;
    const period = kind === 'weekly' ? 'Нед.' : Number.isFinite(duration) && duration > 0 ? duration % 60 === 0 ? `${duration / 60} ч` : `${duration} мин` : '—';
    return { period, value: known ? `${remaining}%` : '—', remaining,
      date: reset ? date.format(reset) : '—', time: reset ? time.format(reset) : '—', marker: known && stale ? 'устар.' : '',
      tone: !known ? 'unknown' : remaining <= 10 ? 'critical' : remaining <= 30 ? 'warning' : 'normal' };
  }) };
}
