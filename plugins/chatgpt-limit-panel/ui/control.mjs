const pending = new Map();
let sequence = 0, snapshot, busy = false, connected = false, disposed = false, heartbeat;
const status = document.getElementById('status');
const startButton = document.getElementById('start');
const stopButton = document.getElementById('stop');
const refreshButton = document.getElementById('refresh');
const updateButton=document.getElementById('check-updates'),releaseButton=document.getElementById('open-release'),updateStatus=document.getElementById('update-status');
let updateBusy=false,updateSnapshot,openFailure=false;
const copy = {
  ru: {
    checkUpdates:'Проверить обновления',openRelease:'Открыть релиз',checkingUpdates:'Проверка обновлений…',
    updateAvailable:(latest,current)=>`Доступна ChatGPT Limit Panel ${latest}. Установлена: ${current}.`,
    updateCurrent:current=>`Установлена актуальная ChatGPT Limit Panel ${current}.`,
    updateAhead:(current,latest)=>`Установленная версия ${current} новее последнего релиза ${latest}.`,
    updateError:'Не удалось проверить обновления. Проверь соединение и повтори попытку.',
    updateRateLimit:'Достигнут лимит запросов GitHub. Повтори проверку позже.',updateTimeout:'Время проверки обновлений истекло. Повтори попытку.',
    openError:'Не удалось открыть страницу релиза.',
    start: 'Запустить', stop: 'Остановить', refresh: 'Обновить состояние', connecting: 'Подключение к монитору…',
    running: 'Монитор работает.', stopped: 'Монитор остановлен.', paused: 'Обновление приостановлено.',
    failure: 'Не удалось связаться с монитором. Проверь установку плагина и повтори попытку.',
    timeout: 'Время ожидания ответа истекло.', unavailable: 'Состояние монитора недоступно.', closed: 'Карточка закрыта.', connection: 'Ошибка соединения.',
    hostError: 'Не удалось подключиться к ChatGPT. Открой плагин с локальным исполнителем.',
    notice: 'При первом запуске настраивается автозапуск при входе в Windows. Панель ждёт открытия ChatGPT и появляется примерно через 3 секунды. Автозапуск можно отключить через ПКМ по панели. «Остановить» завершает текущий монитор; настройка автозапуска сохраняется. Windows x64, локальный исполнитель.',
  },
  en: {
    checkUpdates:'Check for updates',openRelease:'Open release',checkingUpdates:'Checking for updates…',
    updateAvailable:(latest,current)=>`ChatGPT Limit Panel ${latest} is available. Installed: ${current}.`,
    updateCurrent:current=>`ChatGPT Limit Panel ${current} is up to date.`,
    updateAhead:(current,latest)=>`Installed ${current} is newer than the latest release ${latest}.`,
    updateError:'Could not check for updates. Check your connection and try again.',
    updateRateLimit:'GitHub request limit reached. Try again later.',updateTimeout:'The update check timed out. Try again.',
    openError:'Could not open the release page.',
    start: 'Start', stop: 'Stop', refresh: 'Refresh status', connecting: 'Connecting to the monitor…',
    running: 'Monitor is running.', stopped: 'Monitor is stopped.', paused: 'Updates are paused.',
    failure: 'Could not connect to the monitor. Check the plugin installation and try again.',
    timeout: 'The request timed out.', unavailable: 'Monitor status is unavailable.', closed: 'The control card is closed.', connection: 'Connection error.',
    hostError: 'Could not connect to ChatGPT. Open the plugin with a local executor.',
    notice: 'The first start configures automatic launch at Windows sign-in. The panel waits for ChatGPT to open and appears after about 3 seconds. Right-click the panel to disable autostart. Stop closes the current monitor; the autostart setting is preserved. Windows x64, local executor required.',
  },
};
let language = document.documentElement.lang === 'en' ? 'en' : 'ru';
const t = key => copy[language][key];
function applyLanguage(value) {
  if (value !== 'ru' && value !== 'en') return;
  language = value; document.documentElement.lang = language;
  startButton.textContent = t('start'); stopButton.textContent = t('stop'); refreshButton.textContent = t('refresh');
  document.getElementById('notice').textContent = t('notice');
  updateButton.textContent=t('checkUpdates');releaseButton.textContent=t('openRelease');renderUpdate();
}
applyLanguage(language); status.textContent = t('connecting');

function rpc(method, params = {}) {
  return new Promise((resolve, reject) => {
    const id = ++sequence;
    const timer = setTimeout(() => { pending.delete(id); reject(new Error(t('timeout'))); }, 12000);
    pending.set(id, { resolve, reject, timer });
    window.parent.postMessage({ jsonrpc: '2.0', id, method, params }, '*');
  });
}

function applyHostContext(context) {
  if (!context) return;
  if (['dark', 'light'].includes(context.theme)) document.documentElement.style.colorScheme = context.theme;
  for (const [name, value] of Object.entries(context.styles?.variables || {})) if (name.startsWith('--') && typeof value === 'string') document.documentElement.style.setProperty(name, value);
}

function buttons() {
  const disabled = busy || !connected || disposed;
  startButton.disabled = disabled || snapshot?.running === true;
  stopButton.disabled = disabled || snapshot?.running === false;
  refreshButton.disabled = disabled;
  updateButton.disabled=updateBusy||!connected||disposed;
}
function safeReleaseUrl(url) {return typeof url==='string'&&url.length<200&&url===url.trim()&&/^https:\/\/github\.com\/MrBogomoll\/[A-Za-z0-9][A-Za-z0-9_.-]{0,99}\/releases\/tag\/v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(url);}
function renderUpdate() {
  const value=updateSnapshot;releaseButton.hidden=updateBusy||value?.state!=='available'||!safeReleaseUrl(value.url);
  if(updateBusy){updateStatus.textContent=t('checkingUpdates');return;}
  if(!value){updateStatus.textContent='';return;}
  updateStatus.textContent=openFailure?t('openError'):value.state==='available'&&safeReleaseUrl(value.url)?t('updateAvailable')(value.latestVersion,value.currentVersion):value.state==='current'?t('updateCurrent')(value.currentVersion):value.state==='ahead'?t('updateAhead')(value.currentVersion,value.latestVersion):t(value.reason==='rate-limit'?'updateRateLimit':value.reason==='timeout'?'updateTimeout':'updateError');
}
async function checkUpdates() {
  if(updateBusy||disposed||!connected)return;
  updateBusy=true;openFailure=false;buttons();renderUpdate();
  try {
    const result=await rpc('tools/call',{name:'check_quota_panel_updates',arguments:{}});
    if(result?.isError||!result?.structuredContent?.update)throw new Error();
    if(!disposed){updateSnapshot=result.structuredContent.update;applyLanguage(result.structuredContent.language);}
  }catch{if(!disposed)updateSnapshot={state:'error',reason:'network'};}
  finally{updateBusy=false;if(!disposed){renderUpdate();buttons();}}
}
updateButton.addEventListener('click',checkUpdates);
releaseButton.addEventListener('click',async()=>{
  if(disposed||!safeReleaseUrl(updateSnapshot?.url))return;
  releaseButton.disabled=true;
  try{const result=await rpc('ui/open-link',{url:updateSnapshot.url});if(result?.isError)throw new Error();openFailure=false;}
  catch{openFailure=true;}
  finally{releaseButton.disabled=false;if(!disposed)renderUpdate();}
});

function showState(value) {
  if (!value || typeof value.running !== 'boolean' || !['running', 'stopped'].includes(value.status)) throw new Error(t('unavailable'));
  snapshot = value;
  applyLanguage(value.language);
  status.textContent = t(value.paused ? 'paused' : value.running ? 'running' : 'stopped');
  buttons();
}

async function call(name) {
  if (busy || disposed || !connected) return;
  busy = true; buttons();
  try {
    const result = await rpc('tools/call', { name, arguments: {} });
    if (result?.isError) throw new Error(t('failure'));
    if (!disposed) showState(result?.structuredContent);
  } catch {
    if (!disposed) status.textContent = t('failure');
  } finally { busy = false; buttons(); }
}

function dispose() {
  disposed = true; clearInterval(heartbeat); buttons();
  for (const request of pending.values()) { clearTimeout(request.timer); request.reject(new Error(t('closed'))); }
  pending.clear();
}

window.addEventListener('message', event => {
  if (event.source !== window.parent || event.data?.jsonrpc !== '2.0') return;
  const message = event.data;
  if (message.id !== undefined && pending.has(message.id)) {
    const request = pending.get(message.id); pending.delete(message.id); clearTimeout(request.timer);
    message.error ? request.reject(new Error(t('connection'))) : request.resolve(message.result);
  } else if (message.method === 'ui/notifications/host-context-changed') applyHostContext(message.params);
  else if (message.method === 'ui/notifications/tool-result' && message.params?.structuredContent && !busy && !disposed) {
    try {const value=message.params.structuredContent;if(value.update&&!updateBusy){updateSnapshot=value.update;openFailure=false;applyLanguage(value.language);renderUpdate();}else showState(value);} catch { /* Ignore unrelated or incomplete host results. */ }
  } else if (message.method === 'ui/resource-teardown') {
    dispose();
    if (message.id !== undefined) window.parent.postMessage({ jsonrpc: '2.0', id: message.id, result: {} }, '*');
  }
});

startButton.addEventListener('click', () => call('start_quota_overlay'));
stopButton.addEventListener('click', () => call('stop_quota_overlay'));
refreshButton.addEventListener('click', () => call('get_quota_overlay_status'));
window.addEventListener('pagehide', dispose, { once: true });

try {
  const result = await rpc('ui/initialize', { protocolVersion: '2026-01-26', appInfo: { name: 'chatgpt-limit-panel', version: '1.4.4-beta.5' }, appCapabilities: {} });
  if (!disposed) {
    applyHostContext(result?.hostContext); connected = true;
    window.parent.postMessage({ jsonrpc: '2.0', method: 'ui/notifications/initialized' }, '*');
    await call('get_quota_overlay_status');
    if (!disposed) heartbeat = setInterval(() => call('get_quota_overlay_status'), 5000);
  }
} catch {
  if (!disposed) status.textContent = t('hostError');
}
