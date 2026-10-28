# ChatGPT Limit Panel

[Русский](README.md)

<img src="plugins/chatgpt-limit-panel/assets/chatgpt-limit-panel.png" width="88" height="88" alt="ChatGPT Limit Panel">

A Windows panel for the shared Codex / Work usage limits in desktop ChatGPT. Shows remaining percentages and reset times beside the app, with an optional two-row indicator on the Windows taskbar. Instant displays the same Codex / Work counters.

**[Download ChatGPT Limit Panel 1.4.0 for Windows x64](https://github.com/MrBogomoll/chatgpt-limit-panel/releases/download/v1.4.0/chatgpt-limit-panel-1.4.0-windows-x64.zip)** · [All releases](https://github.com/MrBogomoll/chatgpt-limit-panel/releases) · [Upgrade an installed panel](UPGRADING.md)

Choose **chatgpt-limit-panel-1.4.0-windows-x64.zip** under Assets. The complete ZIP includes the programs, Node.js and its license. Git and a separate Node.js installation are not required. GitHub's automatic **Source code** archives contain repository materials.

## New in 1.4.0

- Drag the taskbar indicator along the taskbar or the sidebar panel vertically, with saved positions and occupied-control clearance.
- Fix free-space detection on vertical taskbars with Start at the top, including redundant classic Start11 layout occupancy.
- Reduce ChatGPT window-tracking overhead and keep the working independent, nonactivating sidebar window.

Known resizing and dragging limitations are listed in [KNOWN-ISSUES.md](docs/KNOWN-ISSUES.md). Ultracompact view remains available manually through the menu or view cycling.

## Fixed in 1.3.2

- Update checks now use the primary public repository containing source and releases.
- Versions 1.3.1 and earlier need a one-time manual upgrade: [instructions](UPGRADING.md).

## Features introduced in 1.3.0

- An optional Windows taskbar indicator: five-hour remaining percentage above weekly remaining percentage, with left or right placement.
- Stable taskbar attachment when switching between ChatGPT and games or opening Start and Start11 menus.
- Continued limit refresh with the ChatGPT window closed while the taskbar indicator is enabled.
- A 0–100% background transparency slider in the indicator's right-click menu. Percentages stay readable and the menu remains accessible with a transparent background.
- Consistent program, icon and build filenames, with manual GitHub update checks.

## Preview

| Theme | Full | Compact | Ultracompact |
| :--- | :---: | :---: | :---: |
| Dark | <img src="images/full-dark.png" width="80" alt="Full dark"> | <img src="images/compact-dark.png" width="80" alt="Compact dark"> | <img src="images/ultracompact-dark.png" width="80" alt="Ultracompact dark"> |
| Light | <img src="images/full-light.png" width="80" alt="Full light"> | <img src="images/compact-light.png" width="80" alt="Compact light"> | <img src="images/ultracompact-light.png" width="80" alt="Ultracompact light"> |

### Beside the chat

<img src="images/sidebar-dark.png" width="360" alt="Dark sidebar placement"> <img src="images/sidebar-light.png" width="360" alt="Light sidebar placement">

### On the Windows taskbar

<img src="images/taskbar-dark.png" width="1000" alt="Dark taskbar, five-hour percentage above weekly percentage">

<img src="images/taskbar-light.png" width="1000" alt="Light taskbar with the limit indicator">

The panels are rendered from the actual application controls. Values are demonstration data; the surrounding chat and taskbar are schematic illustrations, not desktop screenshots.

## Features

- Server-provided remaining percentages and reset timestamps, refreshed about every 30 seconds. Stale and unknown data are distinguished from current values.
- Three sidebar sizes, cycled with left-click or selected through the right-click menu.
- Bars, outline, both, or no graphics. Ultracompact always keeps its two bars.
- One shared five-hour or weekly source for the outline in every view.
- Muted orange at ≤30% remaining, red at ≤10%. A known zero fills enabled indicators red.
- Dark, Light and Auto themes, with Russian and English interfaces.
- Saved language, theme, size, transparency, indicators and autostart preference.
- Taskbar placement uses a verified free gap, accounting for app buttons, tray, clock and adjacent widgets.

## Installation

Requires Windows x64, a desktop ChatGPT client supporting local plugins, and installed Codex signed in to the intended account. No API key is required.

1. Download the built ZIP above.
2. Open **Plugins → Add → Upload plugin archive** and select the ZIP.
3. Open **ChatGPT Limit Panel** and its control card.
4. Click **Start** once.

First activation configures per-user Windows autostart without administrator credentials or a password. The sidebar panel appears about three seconds after detecting ChatGPT. Enable the taskbar indicator separately: right-click the panel → **Taskbar → Show percentages**.

With the taskbar indicator enabled, limits continue refreshing with the ChatGPT window closed. Otherwise the monitor waits for the window. Closing the control card keeps the panel running. **Stop panel** explicitly ends the current monitor.

## Controls and updates

Right-click the sidebar panel for view, indicators, outline source, theme, language, autostart, refresh and stop. Right-click the taskbar indicator for placement and background transparency.

**Check for a new version** explicitly checks the latest stable release. **Open release** takes you to its download page when a newer version is available. Updating is manual. Network failures and GitHub request limits are reported separately.

## Compatibility

Actually checked on Windows 11, including Start11 and an adjacent NetWorx indicator. A physical Windows 10 check has not been performed. If ZIP import is unavailable or an error occurs, include the Windows version, client version and reproduction steps in [Issues](https://github.com/MrBogomoll/chatgpt-limit-panel/issues).

## Checksum and data

[SHA-256](https://github.com/MrBogomoll/chatgpt-limit-panel/releases/download/v1.4.0/chatgpt-limit-panel-1.4.0-windows-x64.zip.sha256):

```text
f9d407f06e4a31e0b10f55a1bbde04a663a082684aa3b0908a75f749c23ccddc
```

The panel uses your own local Codex sign-in. Author credentials, conversations and account settings are not included. ChatGPT files are unchanged. ChatGPT Limit Panel is an independent project, not an official OpenAI product.

## Source and development

This repository contains the application source, tests, build tools and documentation previews. See [building and testing](docs/BUILDING.md), [architecture](docs/ARCHITECTURE.md) and [contribution guide](CONTRIBUTING.md). The default branch is `main`; experimental branches use `beta/`.
