import { englishTools, connectionError } from './language.mjs';
import { UI_URI } from './ui.mjs';
import {createUpdateChecker,updateMessage} from './updates.mjs';
export { UI_URI };
const emptyArguments = { type: 'object', properties: {}, additionalProperties: false };
const readOnly = { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false };
const mutation = { ...readOnly, readOnlyHint: false };
const tools = [
  { name: 'show_quota_overlay', title: 'Открыть управление панелью лимитов', description: 'Открывает кнопки запуска и остановки локального монитора общего лимита Codex/Work. Читает состояние; сам монитор не запускает. Требует Windows x64 и локальный исполнитель.', inputSchema: emptyArguments, annotations: readOnly,
    _meta: { ui: { resourceUri: UI_URI }, 'openai/outputTemplate': UI_URI, 'openai/ui': { entrypoints: [{ type: 'global' }, { type: 'thread' }] }, 'openai/widgetAccessible': true } },
  { name: 'start_quota_overlay', title: 'Запустить панель лимитов', description: 'Запускает один локальный монитор лимитов. При первой активации настраивает пользовательскую задачу автозапуска при входе в Windows; монитор ждёт открытия ChatGPT. Автозапуск отключается через ПКМ по панели. Повторный запуск не создаёт дубликат.', inputSchema: emptyArguments, annotations: mutation, _meta: { 'openai/widgetAccessible': true } },
  { name: 'stop_quota_overlay', title: 'Остановить панель лимитов', description: 'Кооперативно закрывает только локальный монитор лимитов. Повторная остановка безопасна. Клиент ChatGPT не закрывает.', inputSchema: emptyArguments, annotations: mutation, _meta: { 'openai/widgetAccessible': true } },
  { name: 'get_quota_overlay_status', title: 'Обновить состояние панели', description: 'Проверяет, запущен ли локальный монитор лимитов. Перекрытая другим окном или скрытая при сворачивании панель продолжает считаться запущенной.', inputSchema: emptyArguments, annotations: readOnly, _meta: { ui: { visibility: ['app'] }, 'openai/widgetAccessible': true } },
  {name:'check_quota_panel_updates',title:'Проверить обновления ChatGPT Limit Panel',description:'Вручную проверяет последний стабильный выпуск в публичном GitHub. Возвращает доступную версию и ссылку на релиз; не устанавливает обновление и не запускает монитор.',inputSchema:emptyArguments,annotations:{...readOnly,openWorldHint:true},_meta:{'openai/widgetAccessible':true}},
];
const methodFor = { show_quota_overlay: 'status', start_quota_overlay: 'start', stop_quota_overlay: 'stop', get_quota_overlay_status: 'status' };

export function createMcpHandler({ controller, readUi, getLanguage = () => 'ru',updateChecker=createUpdateChecker() }) {
  return async request => {
    if (request?.id === undefined) return null;
    const respond = result => ({ jsonrpc: '2.0', id: request.id, result });
    const error = (code, message) => ({ jsonrpc: '2.0', id: request.id, error: { code, message } });
    switch (request.method) {
      case 'initialize': return respond({ protocolVersion: request.params?.protocolVersion || '2025-06-18', capabilities: { tools: {}, resources: {} }, serverInfo: { name: 'chatgpt-limit-panel', title: 'ChatGPT Limit Panel', version: '1.6.0-beta.3' } });
      case 'ping': return respond({});
      case 'tools/list': return respond({ tools: getLanguage() === 'en' ? tools.map(tool => ({ ...tool, title: englishTools[tool.name][0], description: englishTools[tool.name][1] })) : tools });
      case 'resources/list': return respond({ resources: [{ uri: UI_URI, name: getLanguage() === 'en' ? 'ChatGPT Limit Panel controls' : 'Управление панелью лимитов', mimeType: 'text/html;profile=mcp-app' }] });
      case 'resources/templates/list': return respond({ resourceTemplates: [] });
      case 'resources/read': {
        const requestedUri = request.params?.uri;
        // Old saved conversations may still ask for the original card address.
        // Resolve that one compatibility alias to the current translated UI.
        if (requestedUri !== UI_URI && requestedUri !== 'ui://quota-panel/control-v1.html') return error(-32602, 'Unknown UI resource');
        return respond({ contents: [{ uri: requestedUri, mimeType: 'text/html;profile=mcp-app', text: await readUi(), _meta: { ui: { prefersBorder: true, csp: { connectDomains: [], resourceDomains: [] } }, 'openai/widgetDescription': getLanguage() === 'en' ? 'Controls a standalone local quota monitor: start, stop, and refresh status. Closing this card does not stop the monitor.' : 'Управление самостоятельным локальным монитором квот: запуск, остановка и проверка состояния. Закрытие карточки не останавливает монитор.' } }] });
      }
      case 'tools/call': {
        const name = request.params?.name;
        const args = request.params?.arguments === undefined ? {} : request.params.arguments;
        if(name==='check_quota_panel_updates'&&args&&typeof args==='object'&&!Array.isArray(args)&&!Object.keys(args).length) {
          const update=await updateChecker.check(),language=getLanguage(),message=updateMessage(update,language);
          return respond({structuredContent:{update,language},content:[{type:'text',text:message}]});
        }
        if (!Object.hasOwn(methodFor, name) || typeof args !== 'object' || args === null || Array.isArray(args) || Object.keys(args).length) return error(-32602, 'Unknown tool or arguments');
        try {
          const state = await controller[methodFor[name]]();
          return respond({ structuredContent: state, content: [{ type: 'text', text: state.message }] });
        } catch {
          return respond({ isError: true, content: [{ type: 'text', text: connectionError(getLanguage()) }] });
        }
      }
      default: return error(-32601, 'Method not found');
    }
  };
}
