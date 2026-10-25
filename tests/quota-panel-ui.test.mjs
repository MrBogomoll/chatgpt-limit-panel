import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import path from 'node:path';
import { mkdir } from 'node:fs/promises';
const api = await import('../plugins/quota-panel/server/ui.mjs').catch(() => ({}));
const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

test('control card starts and stops the monitor, refreshes state, and never publishes composer context', async () => {
  assert.equal(typeof api.buildUi, 'function', 'native panel control UI must exist');
  const ui = await api.buildUi();
  const host = `<iframe src="/control" style="width:100%;height:300px"></iframe><script>
    window.calls=[];window.running=false;window.fail=false;window.language="ru";
    addEventListener('message',event=>{const m=event.data;if(m?.jsonrpc!=='2.0')return;calls.push(m);let result={};
      if(m.method==='ui/initialize')result={protocolVersion:'2026-01-26',hostContext:{theme:'dark'},hostCapabilities:{},hostInfo:{name:'test',version:'1'}};
      if(m.method==='tools/call'){
        if(fail) result={isError:true,content:[{type:'text',text:'Не удалось связаться с монитором.'}]};
        else {const previous=running;if(m.params.name==='start_quota_overlay')running=true;if(m.params.name==='stop_quota_overlay')running=false;
        result={structuredContent:{status:running?'running':'stopped',running,changed:previous!==running,visible:false,paused:false,language:window.language,message:running?'Монитор работает.':'Монитор остановлен.'}};}
      }
      if(m.id!==undefined)event.source.postMessage({jsonrpc:'2.0',id:m.id,result},'*');
    });
  </script>`;
  const server = createServer((request, response) => { response.setHeader('content-type', 'text/html; charset=utf-8'); response.end(request.url === '/control' ? ui : host); });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage();
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.goto(`http://127.0.0.1:${server.address().port}`);
    const frame = page.frameLocator('iframe');
    await frame.getByRole('heading', { name: 'Quota Panel', exact: true }).waitFor();
    await frame.getByRole('status').filter({ hasText: 'Монитор остановлен.' }).waitFor();
    assert.equal(await frame.getByRole('button', { name: 'Запустить', exact: true }).isEnabled(), true);
    assert.equal(await frame.getByRole('button', { name: 'Остановить', exact: true }).isEnabled(), false);
    await frame.getByRole('button', { name: 'Запустить', exact: true }).click();
    await frame.getByRole('status').filter({ hasText: 'Монитор работает.' }).waitFor();
    assert.equal(await frame.getByRole('button', { name: 'Запустить', exact: true }).isEnabled(), false);
    await frame.getByRole('button', { name: 'Обновить состояние', exact: true }).click();
    await page.waitForFunction(() => calls.filter(call => call.params?.name === 'get_quota_overlay_status').length >= 2);
    await frame.getByRole('button', { name: 'Остановить', exact: true }).click();
    await frame.getByRole('status').filter({ hasText: 'Монитор остановлен.' }).waitFor();
    await page.evaluate(() => { language = 'en'; });
    await frame.getByRole('button', { name: 'Обновить состояние', exact: true }).click();
    await frame.getByRole('button', { name: 'Refresh status', exact: true }).waitFor();
    await frame.getByRole('status').filter({ hasText: 'Monitor is stopped.' }).waitFor();
    assert.equal(await frame.locator('html').getAttribute('lang'), 'en');
    assert.match(await frame.locator('#notice').textContent(), /Windows sign-in/);
    await frame.getByRole('button', { name: 'Start', exact: true }).click();
    await frame.getByRole('status').filter({ hasText: 'Monitor is running.' }).waitFor();
    assert.doesNotMatch(await frame.locator('main').textContent(), /[А-Яа-яЁё]/, 'English card must translate every visible button, status and explanatory paragraph');
    if (process.env.QUOTA_CARD_ARTIFACT_DIR) {
      await mkdir(process.env.QUOTA_CARD_ARTIFACT_DIR, { recursive: true });
      await frame.locator('main').screenshot({ path: path.join(process.env.QUOTA_CARD_ARTIFACT_DIR, 'control-english.png') });
    }
    await page.evaluate(() => { fail = true; });
    await frame.getByRole('button', { name: 'Refresh status', exact: true }).click();
    await frame.getByRole('status').filter({ hasText: 'Could not connect' }).waitFor();
    await page.evaluate(() => { fail = false; language = 'ru'; });
    await frame.getByRole('button', { name: 'Refresh status', exact: true }).click();
    await frame.getByRole('button', { name: 'Обновить состояние', exact: true }).waitFor();
    await page.evaluate(() => { fail = true; });
    await frame.getByRole('button', { name: 'Обновить состояние', exact: true }).click();
    await frame.getByRole('status').filter({ hasText: 'Не удалось' }).waitFor();
    assert.equal(await frame.getByRole('button', { name: 'Обновить состояние', exact: true }).isEnabled(), true);
    await page.evaluate(() => { const frameWindow=document.querySelector('iframe').contentWindow;frameWindow.dispatchEvent(new Event('pagehide')); });
    const calls = await page.evaluate(() => window.calls);
    assert(calls.some(call => call.method === 'ui/notifications/initialized'));
    assert(calls.every(call => call.method !== 'ui/update-model-context'));
    assert.equal(calls.filter(call => call.params?.name === 'stop_quota_overlay').length, 1);
    assert.deepEqual(errors, []);
  } finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
});
