import { readFile } from 'node:fs/promises';

export async function buildUi({ language = 'ru' } = {}) {
  const [html, script] = await Promise.all([
    readFile(new URL('../ui/control.html', import.meta.url), 'utf8'),
    readFile(new URL('../ui/control.mjs', import.meta.url), 'utf8'),
  ]);
  return html.replace('<html lang="ru">', `<html lang="${language === 'en' ? 'en' : 'ru'}">`).replace('/* QUOTA_PANEL_BUNDLE */', () => script);
}
