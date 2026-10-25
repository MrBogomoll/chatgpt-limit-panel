import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const execute = promisify(execFile);
const defaultRoot = fileURLToPath(new URL('../', import.meta.url));
const failureMessage = 'Не удалось связаться с монитором. Проверь установку плагина и повтори попытку.';

/** Invoke our cooperative native launcher; never inspect or stop unrelated processes. */
export function createOverlayController({ pluginRoot = defaultRoot, command = join(pluginRoot, 'dist', 'overlay', 'QuotaMonitor.exe'), prefixArgs = [], timeoutMs = 8000, run = execute } = {}) {
  async function invoke(operation) {
    try {
      const { stdout } = await run(command, [...prefixArgs, operation], { windowsHide: true, shell: false, timeout: timeoutMs, encoding: 'utf8', maxBuffer: 16 * 1024 });
      const value = JSON.parse(stdout.replace(/^\uFEFF/, '').trim());
      if (!value || !['running', 'stopped'].includes(value.status) || typeof value.running !== 'boolean' || value.running !== (value.status === 'running') || ['changed', 'visible', 'paused'].some(key => typeof value[key] !== 'boolean') || typeof value.message !== 'string' || value.message.length > 500) throw new Error('Invalid monitor response');
      const state={ status: value.status, running: value.running, changed: value.changed, visible: value.visible, paused: value.paused, message: value.message };
      if(value.language==='ru'||value.language==='en')state.language=value.language;
      if(typeof value.autoStartEnabled==='boolean'&&typeof value.autoStartConfigured==='boolean'){state.autoStartEnabled=value.autoStartEnabled;state.autoStartConfigured=value.autoStartConfigured;}
      return state;
    } catch {
      // Native stderr, paths, and provider diagnostics must not cross the MCP boundary.
      throw new Error(failureMessage);
    }
  }
  return { status: () => invoke('--status-json'), start: () => invoke('--start-json'), stop: () => invoke('--stop-json') };
}
