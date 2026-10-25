import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';

// Keep the advertised identity bound to one immutable snapshot for this server.
// A new HTML or script build must not reuse a host's previously cached resource.
const html = readFileSync(new URL('../ui/control.html', import.meta.url), 'utf8');
const script = readFileSync(new URL('../ui/control.mjs', import.meta.url), 'utf8');
const revision = createHash('sha256').update(html).update('\0').update(script).digest('hex').slice(0, 16);
export const UI_URI = `ui://quota-panel/control-${revision}.html`;

export async function buildUi({ language = 'ru' } = {}) {
  return html.replace('<html lang="ru">', `<html lang="${language === 'en' ? 'en' : 'ru'}">`).replace('/* QUOTA_PANEL_BUNDLE */', () => script);
}
