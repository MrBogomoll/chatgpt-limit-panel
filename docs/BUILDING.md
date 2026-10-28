# Building and testing / Сборка и проверки

## Requirements

- Windows x64 and Node.js 24 or later, with npm.
- .NET Framework with WPF and the framework C# compiler at %WINDIR%/Microsoft.NET/Framework64/v4.0.30319/csc.exe.
- Microsoft Edge for headless browser UI tests.
- Signed-in local Codex is needed only to run the live monitor; ordinary tests use isolated fixtures.

## Commands

Run in the repository root:

```powershell
npm install
npm test
npm run build:overlay
npm run render:previews
npm run package:plugin
```

The test suite runs files sequentially because native window tests share the Windows desktop. Tests use temporary fixtures and do not require your account limits. UI tests use installed Edge in headless mode.

Build output: dist/overlay/ChatGPTLimitPanel.exe and ChatGPTLimitPanelLauncher.exe. Packaging includes the bundled Node runtime and third-party/NODE-LICENSE.txt. Built archives appear under dist/releases; packaging refuses to overwrite an existing archive. Change release metadata consistently before creating a different distributable version.

## Preview images

The renderer uses actual production WPF controls with temporary preferences and demonstration data. It does not display windows, capture the desktop or request account limits. Chat and taskbar surroundings are schematic. Generated PNG files are stored in images/. Native rendering startup signals are isolated from the running monitor.

## Проверки перед изменениями

Сохраняйте текущие настройки и внутренние идентификаторы установленного плагина. Проверяйте изменения в тестах, затем собирайте пакет. Не добавляйте в Git файлы авторизации, личные настройки, диагностические снимки аккаунта или исполняемые сборки. Сборочные и временные файлы находятся в игнорируемом dist/.

Runtime dependencies are bundled; end users do not need npm or Git. These commands explain building from source and do not imply byte-for-byte reproducible binaries.
