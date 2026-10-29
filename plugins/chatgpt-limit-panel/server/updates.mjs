export const CURRENT_VERSION='1.4.4-beta.5';
export const RELEASE_ORIGIN='https://github.com/MrBogomoll/chatgpt-limit-panel';
// Repository names can change. The verified public repository ID stays stable.
const endpoint='https://api.github.com/repositories/1433242334/releases/latest';
function version(value,allowPrerelease=false) {
  if(typeof value!=='string'||value.length>64)throw new Error('Invalid version');
  const parsed=/^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$/.exec(value);
  if(!parsed||parsed[0]!==value||parsed[4]&&(!allowPrerelease||parsed[4].split('.').some(id=>/^\d+$/.test(id)&&id.length>1&&id[0]==='0')))throw new Error('Invalid version');
  return value.replace(/^v/,'');
}
export function compareVersions(left,right) {
  const parts=value=>{const normalized=version(value,true),dash=normalized.indexOf('-');return {core:(dash<0?normalized:normalized.slice(0,dash)).split('.').map(BigInt),pre:dash<0?null:normalized.slice(dash+1).split('.')};};
  const a=parts(left),b=parts(right);
  for(let i=0;i<3;i++)if(a.core[i]!==b.core[i])return a.core[i]>b.core[i]?1:-1;
  if(a.pre===null||b.pre===null)return a.pre===b.pre?0:a.pre===null?1:-1;
  for(let i=0;i<Math.min(a.pre.length,b.pre.length);i++) {
    if(a.pre[i]===b.pre[i])continue;
    const numericA=/^\d+$/.test(a.pre[i]),numericB=/^\d+$/.test(b.pre[i]);
    if(numericA&&numericB)return BigInt(a.pre[i])>BigInt(b.pre[i])?1:-1;
    if(numericA!==numericB)return numericA?-1:1;
    return a.pre[i]>b.pre[i]?1:-1;
  }
  return Math.sign(a.pre.length-b.pre.length);
}
export function createUpdateChecker({currentVersion=CURRENT_VERSION,fetchImpl=globalThis.fetch,timeoutMs=8000,cooldownMs=15000,now=Date.now}={}) {
  currentVersion=version(currentVersion,true);
  let pending,cached,cancelPending,activeAbort,expires=0;
  function check() {
    if(pending)return pending;
    if(cached&&now()<expires)return Promise.resolve(cached);
    activeAbort=new AbortController();
    pending=Promise.resolve().then(async()=>{
      const abort=activeAbort;let timer,timedOut=false;
      const failure=reason=>({state:'error',reason,currentVersion,latestVersion:null,url:null,checkedAt:now()});
      if(abort.signal.aborted)return failure('cancelled');
      try {
        const work=(async()=>{
          const response=await fetchImpl(endpoint,{headers:{Accept:'application/vnd.github+json','User-Agent':'ChatGPT-Limit-Panel/'+currentVersion,'X-GitHub-Api-Version':'2022-11-28'},signal:abort.signal,redirect:'error'});
          if(response.status===403||response.status===429)return failure('rate-limit');
          if(response.status===404)return failure('not-found');
          if(!response.ok)return failure('network');
          const text=await response.text();if(text.length>512*1024)return failure('invalid');
          let release;try{release=JSON.parse(text);}catch{return failure('invalid');}
          try {
            if(release.draft!==false||release.prerelease!==false)throw new Error();
            const canonical=/^https:\/\/api\.github\.com\/repos\/MrBogomoll\/([A-Za-z0-9][A-Za-z0-9_.-]{0,99})\/releases\/([1-9]\d*)$/.exec(release.url);
            if(!canonical||!Number.isSafeInteger(release.id)||String(release.id)!==canonical[2])throw new Error();
            const origin='https://github.com/MrBogomoll/'+canonical[1];
            const latestVersion=version(release.tag_name),url=origin+'/releases/tag/'+release.tag_name;
            if(release.html_url!==url)throw new Error();
            const names=[`chatgpt-limit-panel-${latestVersion}-windows-x64.zip`,`quota-panel-${latestVersion}-windows-x64.zip`];
            const asset=names.map(name=>release.assets?.find(item=>item.name===name)).find(Boolean);
            const filename=asset?.name;
            if(!asset||asset.state!=='uploaded'||!Number.isSafeInteger(asset.size)||asset.size<=0||asset.browser_download_url!==origin+'/releases/download/'+release.tag_name+'/'+filename)throw new Error();
            const compared=compareVersions(latestVersion,currentVersion);
            return {state:compared>0?'available':compared===0?'current':'ahead',currentVersion,latestVersion,url:compared>0?url:null,checkedAt:now()};
          }catch{return failure('invalid');}
        })();
        return await Promise.race([work,new Promise((resolve)=>{cancelPending=()=>{resolve(failure('cancelled'));abort.abort();};timer=setTimeout(()=>{timedOut=true;abort.abort();resolve(failure('timeout'));},timeoutMs);})]);
      }catch{return failure(timedOut?'timeout':'network');}
      finally{clearTimeout(timer);}
    }).then(result=>{if(result.reason!=='cancelled'){cached=result;expires=now()+cooldownMs;}return result;}).finally(()=>{pending=null;cancelPending=null;activeAbort=null;});
    return pending;
  }
  return {check,cancel(){if(cancelPending)cancelPending();else activeAbort?.abort();}};
}
export function updateMessage(update,language='ru') {
  const en=language==='en';
  if(update.state==='available')return en?`ChatGPT Limit Panel ${update.latestVersion} is available. Installed: ${update.currentVersion}.`:`Доступна ChatGPT Limit Panel ${update.latestVersion}. Установлена: ${update.currentVersion}.`;
  if(update.state==='current')return en?`ChatGPT Limit Panel ${update.currentVersion} is up to date.`:`Установлена актуальная ChatGPT Limit Panel ${update.currentVersion}.`;
  if(update.state==='ahead')return en?`Installed ${update.currentVersion} is newer than the latest release ${update.latestVersion}.`:`Установленная версия ${update.currentVersion} новее последнего релиза ${update.latestVersion}.`;
  if(update.reason==='rate-limit')return en?'GitHub request limit reached. Try again later.':'Достигнут лимит запросов GitHub. Повтори проверку позже.';
  if(update.reason==='timeout')return en?'The update check timed out. Try again.':'Время проверки обновлений истекло. Повтори попытку.';
  return en?'Could not check for updates. Check your connection and try again.':'Не удалось проверить обновления. Проверь соединение и повтори попытку.';
}
