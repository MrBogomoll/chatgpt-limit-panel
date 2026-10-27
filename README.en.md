# Quota Panel

[Русская документация](README.md)

A Windows x64 quota monitor that shows the shared Codex / Work quota above the avatar in the ChatGPT desktop navigation rail. The panel is a separate native overlay window; it does not modify the ChatGPT client. The installed plugin provides a control card to start, stop and check the monitor.

**Private preview: 1.2.0. Public stable release: 1.1.0.** Source remains private; Quota Panel has not been submitted to or approved for the OpenAI Plugin Directory.

The 1.2.0 preview adds a two-row percentage display in a measured free taskbar gap: short-window quota above weekly quota. Right-click → **Taskbar → Show percentages**, then **Left** or **Right**. It defaults off. The tray icon provides the same controls with ChatGPT closed; enabled taskbar mode keeps quota polling independent of the chat window.

This is a separate window in verified free space, not a shell-reserved slot. It hides instead of covering occupied or uncertain regions. Initial support covers the primary horizontal bottom taskbar. See the [Windows 10/11 comparison](docs/research/2026-10-27-windows-taskbar-research.md). Windows 10 has not received live acceptance testing.

Public-submission requirements and remaining steps: [Russian publishing guide](docs/PUBLISHING-RU.md).

Version 1.1.0 adds manual update checks. Right-click the panel → **Check for updates**, or use the button in the control card. When a newer stable release is available, choose **Open release**. Git and GitHub sign-in are not required; updates are not installed automatically.

## Language and controls

Right-click the panel → **Language / Язык → English**. The choice persists across restarts and translates the period labels, stale markers, panel and tray menus, launcher messages and plugin control card. The card picks up a change on its next status refresh, normally within 5 seconds. ChatGPT's own language is left unchanged. Russian is the default.

Left-click cycles **Full → Compact → Ultracompact**. Right-click opens settings for the view, theme, indicators, outline source and autostart. Themes are Dark, Light and Auto. Auto follows the saved app theme and falls back to the Windows app theme when appropriate.

The upper row is the 5-hour limit; the lower row is the weekly limit. Compact view keeps the 5-hour percentage and reset time, followed by the weekly percentage and reset date. Ultracompact view always shows two bars without text. Bars and the outline can be enabled together; the same 5-hour or weekly outline source applies to all views.

Remaining quota at or below 30% uses a muted orange warning; at or below 10% it uses muted red. A known zero is a fully red enabled indicator, while the numeric value remains 0%. Unknown data is not filled in. Stale known data is dimmed and marked **stale** in the text views.

Reset dates use **DD.MM** and reset times use **24-hour HH:mm**, in the local timezone. Changing language does not change the quota, the reset timestamps or the numeric display format.

## Requirements and installation

Requires Windows x64, the ChatGPT desktop app with a local executor, and installed local Codex signed in to the account whose limits you want to see. No API key is needed. The local Codex account must match the intended ChatGPT account.

Download the built Windows ZIP and SHA-256 from this private repository's [Releases](https://github.com/MrBogomoll/quota-panel/releases). The built plugin ZIP includes Node.js and its license. GitHub's automatic Source code ZIP is a different archive and does not contain the compiled runtime.

From a source checkout, install an extracted built package with:

```powershell
node overlay/install-plugin.mjs "<extracted complete plugin package>"
```

The installer uses the native Codex CLI from PATH, registers a separate local marketplace and backs up the existing configuration before installation. The built package also includes a compatibility manifest for clients that support ZIP import. Do not copy the authoring-only root plugin.json into the built package; the current desktop parser uses .codex-plugin/plugin.json.

Open **Quota Panel** from the plugin menu, open its control card and click **Start** once. This configures a per-user Windows logon task without administrator credentials. Importing the ZIP alone does not configure Windows. After activation, the monitor starts at Windows sign-in. The navigation-rail panel appears about 3 seconds after ChatGPT opens. If the taskbar indicator is enabled, quota polling continues without an open ChatGPT window; otherwise the monitor waits for that window. Closing the control card does not stop the monitor.

Right-click → **Start with ChatGPT** disables or enables future autostart. **Stop panel** closes the current monitor; it preserves the autostart setting. Disable autostart as well if you want it to stay disabled after the next Windows sign-in.

## Development

Use Node.js 24 x64 and the .NET Framework 4.x compiler included with Windows. The control-card browser test uses Playwright 1.62.1 and installed Microsoft Edge.

```powershell
npm install
npm test
npm run build:overlay
npm run package:plugin
```

The package bundles the Node executable used to run packaging. This release uses Node.js 24.22.1 x64. If changing Node versions, also update third-party/NODE-LICENSE.txt. Build output goes to dist/overlay, and versioned package output goes to dist/releases. Existing archives are not overwritten.

Native tests use their own windows offscreen and do not operate ChatGPT. A separate installed-package smoke check starts and stops this monitor and reads real quota data; it is not part of the ordinary test suite.

## Known limitations and data

Only the supported Windows desktop client is covered. Browser ChatGPT and macOS are not supported. The panel stays on a visible ChatGPT window when focus moves elsewhere and follows its Z-order, so other applications can cover it normally. It hides when ChatGPT is minimized, closed or cloaked. Future client layout changes may require placement updates. Physical monitor switching and every client zoom combination have not been validated.

Migration of the Windows task to a future installed version/path and automatic task cleanup on uninstall still need work before public release. The task definition, trigger toggling and actual installed startup have been checked; a real Windows reboot was not performed in the latest acceptance check.

Quota comes from the normal Codex App Server account/rateLimits/read response, refreshed every 30 seconds. The monitor uses the local user's own signed-in account; no author credentials, conversation history or private account configuration are distributed. Settings and diagnostic images of the panel itself are stored under %LOCALAPPDATA%\ChatGPTQuotaOverlay. The monitor does not take desktop screenshots.

The source is not offered under a public license yet. Bundled third-party components retain their own license notices.
