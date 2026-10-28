import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));

test('embedded manifest enables native layered children without privilege or DPI changes', { skip: process.platform !== 'win32', timeout: 25000 }, async t => {
  const temporary = await mkdtemp(path.join(tmpdir(), 'quota passive order '));
  t.after(() => rm(temporary, { recursive: true, force: true }));
  const framework = path.join(process.env.WINDIR || 'C:/Windows', 'Microsoft.NET/Framework64/v4.0.30319');
  const executable = path.join(temporary, 'manifest-test.exe');
  const compiled = spawnSync(path.join(framework, 'csc.exe'), [
    '/nologo', '/codepage:65001', '/platform:x64', '/target:exe', '/main:LayeredChildManifestTest', `/out:${executable}`,
    `/win32manifest:${path.join(root, 'overlay', 'app.manifest')}`,
    ...['PresentationCore','PresentationFramework','WindowsBase','UIAutomationClient','UIAutomationTypes'].map(name=>'/reference:'+path.join(framework,'WPF',name+'.dll')),
    '/reference:'+path.join(framework,'System.Xaml.dll'),'/reference:'+path.join(framework,'System.Web.Extensions.dll'),
    path.join(root, 'tests', 'overlay-manifest.cs'),
  ], { encoding: 'utf8', windowsHide: true, timeout: 15000 });
  assert.ifError(compiled.error);
  assert.equal(compiled.status, 0, `native ordering compilation failed:\n${compiled.stdout}\n${compiled.stderr}`);
  const result = spawnSync(executable, [], { encoding: 'utf8', windowsHide: true, timeout: 8000 });
  assert.ifError(result.error);
  assert.equal(result.status, 0, `native ordering contract failed:\n${result.stdout}\n${result.stderr}`);
  assert.equal(result.stderr, '');
  assert.match(result.stdout, /PASS native child compatibility manifest/);
  t.diagnostic(result.stdout.trim());
});
