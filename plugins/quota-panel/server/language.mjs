import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { homedir } from 'node:os';

export function createLanguageReader({ settingsPath = join(process.env.LOCALAPPDATA || join(homedir(), 'AppData', 'Local'), 'ChatGPTQuotaOverlay', 'settings.json') } = {}) {
  let previous = 'ru';
  return () => {
    try {
      const value = JSON.parse(readFileSync(settingsPath, 'utf8').replace(/^\uFEFF/, ''));
      previous = value?.language === 'en' ? 'en' : 'ru';
    } catch (error) {
      // A native settings write may be in progress. Keep the last valid choice.
      if (error.code === 'ENOENT') previous = 'ru';
    }
    return previous;
  };
}

export const englishTools = {
  show_quota_overlay: ['Open quota panel controls', 'Opens controls to start and stop the local Codex/Work quota monitor. Reads status without starting the monitor. Requires Windows x64 and a local executor.'],
  start_quota_overlay: ['Start quota panel', 'Starts one local quota monitor. First activation configures a per-user Windows logon task; the monitor waits for ChatGPT to open. Autostart can be disabled in the panel context menu. Repeated starts do not create duplicates.'],
  stop_quota_overlay: ['Stop quota panel', 'Cooperatively closes only the local quota monitor. Repeated stops are safe. Does not close ChatGPT.'],
  get_quota_overlay_status: ['Refresh panel status', 'Checks whether the local quota monitor is running. A panel covered by another window or hidden when ChatGPT is minimized still counts as running.'],
};
export const connectionError = language => language === 'en'
  ? 'Could not connect to the monitor. Check the plugin installation and try again.'
  : 'Не удалось связаться с монитором. Проверь установку плагина и повтори попытку.';
