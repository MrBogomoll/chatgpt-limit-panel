const pending = new Map();
let sequence = 0, snapshot, busy = false, connected = false, disposed = false, heartbeat;
const status = document.getElementById('status');
const startButton = document.getElementById('start');
const stopButton = document.getElementById('stop');
const refreshButton = document.getElementById('refresh');

function rpc(method, params = {}) {
  return new Promise((resolve, reject) => {
    const id = ++sequence;
    const timer = setTimeout(() => { pending.delete(id); reject(new Error('Время ожидания ответа истекло.')); }, 12000);
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
}

function showState(value) {
  if (!value || typeof value.running !== 'boolean' || !['running', 'stopped'].includes(value.status)) throw new Error('Состояние монитора недоступно.');
  snapshot = value;
  status.textContent = value.message || (value.running ? 'Монитор работает.' : 'Монитор остановлен.');
  buttons();
}

async function call(name) {
  if (busy || disposed || !connected) return;
  busy = true; buttons();
  try {
    const result = await rpc('tools/call', { name, arguments: {} });
    if (result?.isError) throw new Error('Не удалось связаться с монитором. Проверь установку плагина и повтори попытку.');
    if (!disposed) showState(result?.structuredContent);
  } catch {
    if (!disposed) status.textContent = 'Не удалось связаться с монитором. Проверь установку плагина и повтори попытку.';
  } finally { busy = false; buttons(); }
}

function dispose() {
  disposed = true; clearInterval(heartbeat); buttons();
  for (const request of pending.values()) { clearTimeout(request.timer); request.reject(new Error('Карточка закрыта.')); }
  pending.clear();
}

window.addEventListener('message', event => {
  if (event.source !== window.parent || event.data?.jsonrpc !== '2.0') return;
  const message = event.data;
  if (message.id !== undefined && pending.has(message.id)) {
    const request = pending.get(message.id); pending.delete(message.id); clearTimeout(request.timer);
    message.error ? request.reject(new Error('Ошибка соединения.')) : request.resolve(message.result);
  } else if (message.method === 'ui/notifications/host-context-changed') applyHostContext(message.params);
  else if (message.method === 'ui/notifications/tool-result' && message.params?.structuredContent && !busy && !disposed) {
    try { showState(message.params.structuredContent); } catch { /* Ignore unrelated or incomplete host results. */ }
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
  const result = await rpc('ui/initialize', { protocolVersion: '2026-01-26', appInfo: { name: 'quota-panel', version: '0.2.0-beta.3' }, appCapabilities: {} });
  if (!disposed) {
    applyHostContext(result?.hostContext); connected = true;
    window.parent.postMessage({ jsonrpc: '2.0', method: 'ui/notifications/initialized' }, '*');
    await call('get_quota_overlay_status');
    if (!disposed) heartbeat = setInterval(() => call('get_quota_overlay_status'), 5000);
  }
} catch {
  if (!disposed) status.textContent = 'Не удалось подключиться к ChatGPT. Открой плагин с локальным исполнителем.';
}
