# Quota Panel

Private preview **1.2.1-beta.1**: a targeted fix for taskbar placement after a borderless game leaves a passive NVIDIA Overlay canvas on screen. Actual game transitions are still being verified. The prepared 1.2.0 archive is unchanged.

[Русская документация](README.md)

A Windows x64 monitor for the shared Codex / Work quota. The main panel appears above the avatar in the ChatGPT desktop navigation rail. An optional taskbar indicator shows two remaining percentages. Both displays use the same account quota.

**Release candidate build: 1.2.0. Latest public stable release: 1.1.0.** Preparing the 1.2.0 archive does not publish it. Source remains private; Quota Panel has not been published in the OpenAI Plugin Directory.

## New in 1.2.0

- An optional two-row indicator in a verified free area of the primary Windows taskbar: 5-hour remaining percentage above weekly remaining percentage. Choose a preferred left or right placement among safe gaps.
- Quota polling continues with the ChatGPT window closed while the taskbar indicator is enabled. The Quota Panel tray icon provides controls with the main panel hidden.
- A shared 0–100% background-transparency slider in the taskbar indicator menu. Text and graphics retain their opacity; the empty indicator area remains available for right-clicks with a fully clear background.
- Refined handling of the active Start11 context menu: an already visible indicator retains its verified position while the menu blocks taskbar-button geometry queries.
- A corrected race that could incorrectly mark a fresh taskbar measurement as expired.

[Russian and English release notes](docs/RELEASE-1.2.0.md).

## Features and controls

The shared 5-hour and weekly quota is available across supported ChatGPT window modes, including Instant. The plugin does not request a separate Instant quota. It reads remaining percentages and server reset timestamps through the normal Codex App Server, refreshing about every 30 seconds.

Open **Quota Panel** from the plugin menu and open its control card to start, stop or check the monitor, or manually check for a new version. Closing the card does not stop the monitor.

Left-click cycles **Full → Compact → Ultracompact**. Right-click provides direct view selection, theme, language, indicator style, outline source and autostart. The upper row is the 5-hour limit; the lower row is weekly. Compact view keeps the 5-hour percentage and reset time, then the weekly percentage and reset date. Ultracompact view always shows two bars without text.

Choose bars, an outline, both, or no graphics. The same 5-hour or weekly outline source applies to all views. In ultracompact view the bars remain visible and the shared setting controls the additional coloured outline. A known zero uses a fully red enabled indicator; unknown values remain unfilled and use a dash. Quota at or below 30% uses muted orange, and at or below 10% muted red. Stale known values are dimmed and marked **stale**.

For the taskbar, use **Taskbar → Show percentages**, then **Left** or **Right**. It defaults off. Right-click the indicator → **Background transparency** to change both display backgrounds; the default is 50%. Equivalent controls are available through the tray icon.

Themes are **Dark**, **Light** and **Auto**. Auto follows the saved ChatGPT theme and the Windows app theme when appropriate. Right-click → **Language / Язык → English / Русский** translates panel, tray, card and messages. Settings persist without changing ChatGPT itself. The card picks up language changes on its next status refresh, normally within 5 seconds. Dates use **DD.MM**, times use local **24-hour HH:mm**. Russian is the default.

**Check for a new version** checks stable public GitHub releases; **Pause quota refresh** pauses quota polling. When a newer version is available, **Open release** opens its download page. Installation remains manual. Checking does not require Git or GitHub sign-in.

## Requirements and installation

Requires Windows x64, the ChatGPT desktop app with a local executor, and local Codex signed in to the intended account. The local Codex account must match the account whose limits you want to see. No API key is needed.

Published built archives and SHA-256 files are available from [public Releases](https://github.com/MrBogomoll/quota-panel-chatgpt/releases). Use the separately prepared 1.2.0 build archive until that release is published. The complete ZIP bundles the monitor, Node.js and its license; recipients do not need a separate Node installation. GitHub's automatic **Source code** archive is different from the built plugin ZIP.

Use the supported local-plugin ZIP import in a compatible desktop client. From a source checkout, an extracted complete package can also be installed with:

```powershell
node overlay/install-plugin.mjs "<extracted complete plugin package>"
```

The installer uses the Codex CLI from PATH, registers a separate local marketplace and backs up the existing configuration. The built package uses `.codex-plugin/plugin.json`; do not manually copy the authoring-only root `plugin.json` into it.

Open **Quota Panel** and click **Start** once. This configures its own per-user Windows logon task without administrator credentials or a password. Importing a ZIP alone does not configure Windows. After activation, the monitor starts at Windows sign-in. The main panel appears about 3 seconds after ChatGPT opens. Enabled taskbar mode keeps quota polling independent of an open ChatGPT window; otherwise the monitor waits for the window.

**Start with ChatGPT** controls future autostart. **Stop panel** closes the current monitor. Disable autostart as well if it should remain stopped after the next Windows sign-in.

## Limitations and data

Known issue in 1.2.0: opening Start over a borderless game can hide the taskbar
indicator, observed with Honkai: Star Rail and Hollow Knight: Silksong. A fix is
planned separately for a subsequent patch.

The panels are separate Windows overlay windows. They do not change ChatGPT files or inject code into Explorer. The main panel follows a visible ChatGPT window, its scale and its Z-order; it hides when that window is minimized, closed or cloaked.

The taskbar indicator uses measured free space on the **primary** taskbar and does not reserve a shell slot. It hides when there is insufficient space, fullscreen activity or uncertain geometry. Support for bottom, top, left and right taskbar edges is experimental. Compatibility with every taskbar modifier, other widget, monitor arrangement and scaling configuration is not guaranteed. Windows 10 has not been tested on a separate computer. See the [Windows 10/11 comparison](docs/research/2026-10-27-windows-taskbar-research.md).

This build covers the currently recognised Windows desktop client with a local executor. Browser ChatGPT and macOS are not supported. Future client layout changes may require placement updates.

Settings, state and bounded local diagnostics are stored under `%LOCALAPPDATA%\ChatGPTQuotaOverlay`. Panel renders contain only the panel itself; no desktop screenshots are taken. No author authentication, tokens, conversations or private account configuration are distributed. Each recipient uses their own local Codex account. Automatic cleanup of the Windows task when uninstalling the plugin still needs separate verification.

## Development and versioning

The private `quota-panel-code` repository is the source project. Public `quota-panel-chatgpt` holds user materials and built releases. A finished version uses the same number in source, manifest, archive and public release.

Earlier `1.2.x` and `1.3.x` labels were private intermediate builds. Their changes are consolidated into the upcoming 1.2.0. Historical notes remain available as development history. Future previews use labels such as `1.3.0-beta.1`, `beta.2`, then the finished `1.3.0`; this naming policy does not automatically enable a separate beta-delivery channel. See the [release rules](docs/RELEASE_NOTES.md).

Build with Node.js 24 x64 and the Windows .NET Framework 4.x compiler. Control-card browser checks use Playwright 1.62.1 and installed Microsoft Edge.

```powershell
npm install
npm test
npm run build:overlay
npm run package:plugin
```

Native output goes to `dist/overlay`; the package ZIP and checksum go to `dist/releases`. Packaging bundles the Node executable used to run it. Update `third-party/NODE-LICENSE.txt` when changing that runtime. Existing archives are not overwritten.

Tests use temporary directories, a substituted quota source and their own offscreen windows. The separate `overlay/smoke-package.mjs` starts and stops the installed monitor and is not part of the ordinary test suite.

The source is not offered under a public license yet. Third-party components retain their own license notices. Public-submission requirements: [Russian publishing guide](docs/PUBLISHING-RU.md).
