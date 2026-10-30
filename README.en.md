# ChatGPT Limit Panel

[Русский](README.md)

<img src="plugins/chatgpt-limit-panel/assets/chatgpt-limit-panel.png" width="88" height="88" alt="ChatGPT Limit Panel">

A Windows panel showing your remaining **5-hour and weekly Codex / Work limits** and their reset times. It appears over the desktop ChatGPT window; a taskbar indicator and live tray icon are optional. Instant displays the same Codex / Work counters.

**[Download for Windows x64](https://github.com/MrBogomoll/chatgpt-limit-panel/releases/latest)** · [Update](UPGRADING.md) · [Uninstall](UNINSTALL.md) · [All releases](https://github.com/MrBogomoll/chatgpt-limit-panel/releases)

Download the built application ZIP under **Assets**. GitHub's **Source code** archives contain source files and are not plugin installation packages.

## Features

- Remaining limits and reset times from Codex, refreshed about every 30 seconds. Stale and unknown values are marked separately.
- Full, compact and ultracompact views; left-click cycles views and right-click opens settings.
- Pin to the sidebar or drag freely inside the ChatGPT window. The position is saved; Reset position returns the panel to the sidebar area.
- Independent panel background opacity and taskbar indicator transparency. Set panel **background opacity to 100%** to prevent conversation text from showing through.
- Bars and an outline show remaining quota; choose the 5-hour or weekly limit for the outline. Muted orange indicates ≤30% remaining, red ≤10%. Ultracompact always keeps both bars.
- An optional taskbar indicator with percentages and selectable placement, and an optional live tray icon showing the same two limits.
- Dark, light and automatic themes; Russian and English languages. Preferences persist across restarts.



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

## Installation

Requires Windows x64, a desktop ChatGPT client supporting local plugins, and installed Codex signed in to the intended account. A separate API key, Git and Node.js installation are not required.

1. Download the built ZIP using the link above.
2. Open **Plugins → Add → Upload plugin archive** and select the ZIP.
3. Open **ChatGPT Limit Panel** and its control card.
4. Click **Start**.

First activation creates a per-user Windows autostart task; it can be disabled in the panel menu. To enable the taskbar indicator, use **Right-click the panel → Taskbar → Show percentages**. Enable the live icon through **Right-click the tray icon → Live tray icon**.

With the taskbar indicator **or** live tray icon enabled, the monitor can refresh limits without an open ChatGPT window. Closing the control card does not stop the monitor; choose **Stop panel** to stop it.

## Updating and uninstalling

**Check for a new version** checks the stable release and offers to open its page. ZIP installation is manual. Guides: [update](UPGRADING.md) · [complete uninstall](UNINSTALL.md). See [CHANGELOG](CHANGELOG.md) and [releases](https://github.com/MrBogomoll/chatgpt-limit-panel/releases) for the history of changes.

## Compatibility and data

The panel has been checked on Windows 11, including Start11 and placement next to NetWorx. Windows 10 compatibility is not yet confirmed. If ZIP import is unavailable or an error occurs, include the Windows version, client version and details in [Issues](https://github.com/MrBogomoll/chatgpt-limit-panel/issues).

The program uses your existing local Codex sign-in. User credentials and settings are not included in the archive. This is an independent project, not an official OpenAI product. [ZIP checksum for 1.6.1](https://github.com/MrBogomoll/chatgpt-limit-panel/releases/download/v1.6.1/chatgpt-limit-panel-1.6.1-windows-x64.zip.sha256).

## License

The project source code is distributed under the [MIT License](LICENSE). Node.js included in the packaged build has its [own license](third-party/NODE-LICENSE.txt).

## Development

The repository contains source code, tests and build tools. See [building and testing](docs/BUILDING.md), [architecture](docs/ARCHITECTURE.md) and [contribution guide](CONTRIBUTING.md).
