import { readFile } from 'node:fs/promises';

export async function buildUi() {
  const [html, script] = await Promise.all([
    readFile(new URL('../ui/control.html', import.meta.url), 'utf8'),
    readFile(new URL('../ui/control.mjs', import.meta.url), 'utf8'),
  ]);
  return html.replace('/* QUOTA_PANEL_BUNDLE */', () => script);
}
