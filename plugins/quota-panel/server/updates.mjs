export const CURRENT_VERSION='1.3.2';
export const RELEASE_ORIGIN='https://github.com/MrBogomoll/quota-panel-chatgpt';
// Repository names can change. The verified public repository ID stays stable.
const endpoint='https://api.github.com/repositories/1433486830/releases/latest';
function version(value) {
  if(typeof value!=='string'||value.length>64||!/^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(value))throw new Error('Invalid stable version');
  return value.replace(/^v/,'');
}
export function compareVersions(left,right) {
  const a=version(left).split('.').map(BigInt),b=version(right).split('.').map(BigInt);
  for(let i=0;i<3;i++)if(a[i]!==b[i])return a[i]>b[i]?1:-1;
  return 0;
}
export function createUpdateChecker({currentVersion=CURRENT_VERSION,fetchImpl=globalThis.fetch,timeoutMs=8000,cooldownMs=15000,now=Date.now}={}) {
  currentVersion=version(currentVersion);
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
          const response=await fetchImpl(endpoint,{headers:{Accept:'application/vnd.github+json','User-Agent':'Quota-Panel/'+currentVersion,'X-GitHub-Api-Version':'2022-11-28'},signal:abort.signal,redirect:'error'});
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
            const filename=`quota-panel-${latestVersion}-windows-x64.zip`;
            const asset=release.assets?.find(item=>item.name===filename);
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
  if(update.state==='available')return en?`Quota Panel ${update.latestVersion} is available. Installed: ${update.currentVersion}.`:`Доступна Quota Panel ${update.latestVersion}. Установлена: ${update.currentVersion}.`;
  if(update.state==='current')return en?`Quota Panel ${update.currentVersion} is up to date.`:`Установлена актуальная Quota Panel ${update.currentVersion}.`;
  if(update.state==='ahead')return en?`Installed ${update.currentVersion} is newer than the latest release ${update.latestVersion}.`:`Установленная версия ${update.currentVersion} новее последнего релиза ${update.latestVersion}.`;
  if(update.reason==='rate-limit')return en?'GitHub request limit reached. Try again later.':'Достигнут лимит запросов GitHub. Повтори проверку позже.';
  if(update.reason==='timeout')return en?'The update check timed out. Try again.':'Время проверки обновлений истекло. Повтори попытку.';
  return en?'Could not check for updates. Check your connection and try again.':'Не удалось проверить обновления. Проверь соединение и повтори попытку.';
}
