import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { cp, copyFile, mkdtemp, mkdir, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));

function succeeded(result, stage) {
  assert.ifError(result.error);
  assert.equal(result.status, 0, `${stage} failed (signal ${result.signal ?? 'none'}):\n${result.stdout}\n${result.stderr}`);
  assert.equal(result.stderr, '', `${stage} reported an exception: ${result.stderr}`);
}

test('native overlay renders and accepts updates using only isolated own controls and offscreen windows', { skip: process.platform !== 'win32', timeout: 90000 }, async t => {
  const temporary = await mkdtemp(path.join(tmpdir(), 'quota native тест '));
  t.after(() => rm(temporary, { recursive: true, force: true }));
  const framework = path.join(process.env.WINDIR || 'C:/Windows', 'Microsoft.NET/Framework64/v4.0.30319');
  const executable = path.join(temporary, 'overlay-native-test.exe');
  const references = [
    ...['PresentationCore', 'PresentationFramework', 'WindowsBase','UIAutomationClient','UIAutomationTypes'].map(name => path.join(framework, 'WPF', `${name}.dll`)),
    ...['System.Web.Extensions', 'System.Windows.Forms', 'System.Drawing', 'System.Xaml'].map(name => path.join(framework, `${name}.dll`)),
  ];
  const compilation = spawnSync(path.join(framework, 'csc.exe'), [
    '/nologo', '/codepage:65001', '/platform:x64', '/target:exe', '/main:OverlayNativeTest', `/resource:${path.join(root,'overlay/tray.ico')},ChatGPTLimitPanel.TrayIcon`, `/out:${executable}`,
    `/win32manifest:${path.join(root, 'overlay', 'app.manifest')}`,
    ...references.map(reference => `/reference:${reference}`),
    path.join(root, 'overlay', 'Layout.cs'),path.join(root,'overlay','TaskbarLayout.cs'),path.join(root,'overlay','TaskbarWindow.cs'),path.join(root,'overlay','TaskbarChildSurface.cs'),path.join(root,'overlay','TaskbarAttachment.cs'),path.join(root,'overlay','TaskbarTransient.cs'),path.join(root,'overlay','TaskbarMenuLease.cs'),path.join(root,'overlay','TaskbarDiagnostics.cs'),path.join(root,'overlay','TaskbarAppearance.cs'),path.join(root,'overlay/TaskbarPassiveOverlay.cs'), path.join(root, 'overlay', 'Progress.cs'), path.join(root, 'overlay', 'VisualHost.cs'), path.join(root, 'overlay', 'Theme.cs'), path.join(root, 'overlay', 'Preferences.cs'), path.join(root, 'overlay', 'Language.cs'), path.join(root, 'overlay', 'Overlay.cs'), path.join(root, 'tests', 'overlay-native.cs'),
  ], { encoding: 'utf8', windowsHide: true, timeout: 15000 });
  succeeded(compilation, 'native test compilation');

  for (const [scenario, description] of [
    ['floating-native', 'actual unpinned HWND follows its client through drag, resize, minimize and repinning without activation'],
    ['floating-panel', 'unpinned panel uses two-axis client-relative placement and persists independently of the rail'],
    ['live-tray', 'live tray opt in uses actual quota, shared outline, states and cached full-color graphics'],
    ['tray-menu', 'tray uses branded icon and the shared themed accessible menu with live language and settings'],
    ['rail-resize', 'temporary resize geometry conservatively reserves moving rail controls at all supported scales'],
    ['snap-preview', 'recorded Snap preview hides only the sidebar while ordinary drag and unrelated shell windows remain unaffected'],
    ['layout', '100% and bold reset times fit actual available width with current fonts'],
    ['language', 'Russian and English switch live, fit all three sizes and survive restart without changing quota or settings'],
    ['tones', 'both rows apply readable muted warning and critical colours'],
    ['unknown', 'unknown and missing data do not fabricate quota or reset values'],
    ['protocol', 'production stdin reader preserves multiple UTF-8 JSON lines and closes at EOF'],
    ['compact', 'default compact mode fits full-size values, reset labels and one shared stale indicator'],
    ['ultra', 'ultracompact shows two correctly filled bars and a selectable frame without text'],
    ['ultra-settings', 'all three modes cycle and share the contour source and indicator choice'],
    ['shared-indicator', 'one indicator menu controls all presentations while ultra always retains its two bars'],
    ['theme', 'light palette and menu remain readable and dark palette restores without changing quota values'],
    ['theme-auto', 'auto follows the actual app setting, system fallback and manual override without new quota data'],
    ['theme-settings', 'theme choices persist, reject invalid values and preserve unrelated preferences'],
    ['theme-parser', 'only the desktop appearance setting controls auto, with comments, quotes and multiline decoys handled'],
    ['startup-delay', 'panel readiness waits three seconds after each client opening without affecting normal foreground changes'],
    ['toggle', 'registered panel mouse-up toggles mode and saves both preferences across reconstruction'],
    ['settings', 'invalid or missing compact preferences safely default to compact mode'],
    ['input', 'native hit testing accepts panel clicks while mouse activation preserves foreground focus'],
    ['tooltip', 'panel hover has no redundant popup after refresh or mode switching'],
    ['progress', 'full bars and compact outline show the selected remaining quota without fabricating unknown values'],
    ['progress-settings', 'right-click preferences persist one shared indicator and contour-source selection'],
    ['contour', 'rounded contour advances clockwise by actual path length from top centre'],
    ['menu-actions', 'right-click actions invoke refresh, pause, resume and stop without touching other windows'],
    ['full-indicator', 'full mode independently selects bars, off or a full-height short/weekly contour'],
    ['full-migration', 'legacy and invalid settings preserve disabled indicators and safe full-mode defaults'],
    ['menu-placement', 'context menu anchors beside the panel with a gap'],
    ['menu-dismiss', 'outside button-down dismisses the menu while own submenu clicks and stale callbacks do not'],
    ['rail-translation', 'moving document snapshots retain relative rail controls while resize, DPI and unverified mappings remain rejected'],
    ['rail-overflow', 'rail control discovery includes descendants extending outside their parent bounds'],
    ['target-identity', 'repeated trusted-target validation rejects untrusted filenames without process enumeration on the GUI thread'],
    ['menu-layer', 'actual own-window integration preserves open-menu z-order across tracker ticks'],
    ['focus-persistence', 'panel stays on an inactive visible target below covering apps, follows topmost bands and still hides on minimize/close'],
    ['motion', 'rapid movement aligns in the native callback, avoids disk/render work and fills event gaps at frame rate'],
    ['motion-hook', 'real cross-process WinEvent delivery follows an isolated external target without activation'],
    ['updates','manual update controls show localized themed results without changing quotas or opening arbitrary links'],
    ['taskbar-settings','independent taskbar toggle and side persist with language, theme and view preserved'],
    ['taskbar-recreation','destroyed percentage windows recreate on ordinary tracking ticks with live data and settings preserved'],
    ['transparency','the live background slider persists validated percentages in both views without fading text or graphics'],
    ['combined-zero', 'combined mode persists and known zero draws full red warnings distinct from off and unknown'],
    ['resize-aspect', 'mode transitions preserve proportions while the viewport still has the previous height'],
    ['resize-native', 'content changes do not issue a separate live HWND resize before native placement'],
    ['viewport', 'real offscreen WPF window keeps all quota text inside the actual resized native viewport'],
    ['viewport-light', 'light full/compact/ultra graphics fit actual native windows at all supported scales'],
    ['viewport-en', 'English full/compact/ultra labels and graphics fit actual native windows at all supported scales'],
    ['styles', 'own hidden HWND accepts native clicks without activating or appearing in the taskbar'],
  ]) {
    if(process.env.QUOTA_NATIVE_SCENARIO && process.env.QUOTA_NATIVE_SCENARIO!==scenario)continue;
    await t.test(description, async () => {
      const directory = path.join(temporary, scenario);
      await mkdir(directory);
      const initial = { rows: [
        { period: 'initial', value: '—', date: '—', time: '—', marker: '', tone: 'unknown' },
        { period: 'initial', value: '—', date: '—', time: '—', marker: '', tone: 'unknown' },
      ] };
      const final = { rows: [
        { period: '5 ч', value: '100%', remaining:100, date: '31.12', time: '00:00', marker: 'устар.', tone: 'normal' },
        { period: 'Нед.', value: '30%', remaining:30, date: '31.12', time: '23:59', marker: 'устар.', tone: 'warning' },
      ] };
      let scenarioExecutable=executable;
      if(scenario==='target-identity'){scenarioExecutable=path.join(directory,'ChatGPT.exe');await copyFile(executable,scenarioExecutable);}
      const result = spawnSync(scenarioExecutable, [scenario, directory], {
        input: `${JSON.stringify(initial)}\n${JSON.stringify(final)}\n`,
        encoding: 'utf8', windowsHide: true, timeout: 10000,
      });
      succeeded(result, `native ${scenario} test`);
      if (process.env.QUOTA_NATIVE_ARTIFACT_DIR) await cp(directory, path.join(process.env.QUOTA_NATIVE_ARTIFACT_DIR, scenario), { recursive: true });
      assert.match(result.stdout, new RegExp(`PASS native ${scenario === 'tones' ? 'tones' : scenario}`));
      t.diagnostic(result.stdout.split(/\r?\n/).find(line => line.startsWith('PASS native ')));
      // Apply renders only its own detached WPF visual, under this unique directory.
      const preview = await readFile(path.join(directory, 'panel.png'));
      assert.deepEqual([...preview.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
      if (scenario === 'protocol') {
        const commands = result.stdout.split(/\r?\n/).filter(line => line.startsWith('{')).map(line => JSON.parse(line));
        assert.deepEqual(commands, [{ command: 'ready' }, { command: 'exit' }]);
      }
      if (scenario === 'menu-actions') {
        const commands=result.stdout.split(/\r?\n/).filter(line=>line.startsWith('{')).map(line=>JSON.parse(line).command);
        assert.deepEqual(commands,['refresh','pause','resume','exit']);
      }
    });
  }
});
