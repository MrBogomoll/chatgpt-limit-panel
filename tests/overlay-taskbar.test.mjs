import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));

test('taskbar allocation and own offscreen indicator fail closed without probing the live shell', { skip: process.platform !== 'win32', timeout: 25000 }, async t => {
  const temporary = await mkdtemp(path.join(tmpdir(), 'quota taskbar тест '));
  t.after(() => rm(temporary, { recursive: true, force: true }));
  const framework = path.join(process.env.WINDIR || 'C:/Windows', 'Microsoft.NET/Framework64/v4.0.30319');
  const executable = path.join(temporary, 'taskbar-test.exe');
  const references = ['PresentationCore', 'PresentationFramework', 'WindowsBase', 'UIAutomationClient', 'UIAutomationTypes']
    .map(name => path.join(framework, 'WPF', `${name}.dll`));
  references.push(path.join(framework, 'System.Xaml.dll'),path.join(framework, 'System.Web.Extensions.dll'));
  const compiled = spawnSync(path.join(framework, 'csc.exe'), [
    '/nologo', '/codepage:65001', '/platform:x64', '/target:exe', '/main:TaskbarNativeTest', `/out:${executable}`,
    `/win32manifest:${path.join(root, 'overlay', 'app.manifest')}`,
    ...references.map(reference => `/reference:${reference}`),
    path.join(root, 'overlay', 'Theme.cs'), path.join(root, 'overlay', 'TaskbarLayout.cs'), path.join(root, 'overlay', 'TaskbarWindow.cs'), path.join(root, 'overlay', 'TaskbarTransient.cs'), path.join(root, 'overlay', 'TaskbarMenuLease.cs'), path.join(root, 'overlay', 'TaskbarDiagnostics.cs'), path.join(root, 'overlay', 'TaskbarAppearance.cs'), path.join(root, 'tests', 'overlay-taskbar.cs'),
  ], { encoding: 'utf8', windowsHide: true, timeout: 15000 });
  assert.ifError(compiled.error);
  assert.equal(compiled.status, 0, `taskbar test compilation failed:\n${compiled.stdout}\n${compiled.stderr}`);
  const result = spawnSync(executable, [], { encoding: 'utf8', windowsHide: true, timeout: 8000 });
  assert.ifError(result.error);
  assert.equal(result.status, 0, `taskbar behavior failed:\n${result.stdout}\n${result.stderr}`);
  assert.equal(result.stderr, '');
  assert.match(result.stdout, /PASS taskbar allocation and offscreen indicator/);
  t.diagnostic(result.stdout.trim());
});
