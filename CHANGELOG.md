# Changes

## 1.5.0 — 2026-10-30

- Add an optional live tray icon with actual 5-hour and weekly quota bars, shared outline settings, and persisted opt-in.
- Keep live tray quota refreshing independently of ChatGPT window and taskbar visibility, respecting pause and stop.
- Use the branded full-color alpha tray icon and shared themed tray menu.
- Use muted taskbar-menu separators and put Stop panel at the bottom of the tray menu.

## 1.4.4 — 2026-10-29

- Keep the taskbar indicator attached during automatic taskbar hiding and revealing.

## 1.4.3 — 2026-10-29

- Migrate settings and diagnostic files to the ChatGPTLimitPanel directory, preserving preferences and language.
- Simplify the bilingual package replacement guide and remove obsolete version-specific instructions.

## 1.4.2 — 2026-10-29

- Remove sidebar flicker during horizontal and vertical window resizing, including height growth and shrinkage.
- Preserve sidebar visibility when restoring a maximized window.
- Hide the sidebar during the Windows 11 Snap preview and restore it on exit.
- Windows 10 compatibility remains under investigation.

## 1.4.1

- Select ultracompact automatically when a smaller window or measured free gap cannot fit compact view, preserving the preferred view on growth.
- Keep verified relative rail coordinates during pure translation and reject changed/partially acquired geometry.
- Windows 11 Snap preview handling remains under investigation.

## 1.4.0 — 2026-10-28

- Add saved draggable taskbar and sidebar positions with occupied-control clearance.
- Fix vertical taskbar free-space allocation and redundant Start11 layout occupancy.
- Reduce process-identification and accessibility-query overhead.
- Keep the sidebar in its working independent nonactivating window and preserve preferences.
- Record remaining automatic-size and Windows11 dragging/Snap limitations in [known issues](docs/KNOWN-ISSUES.md).

## 1.3.2 — 2026-10-28

- Check updates against the primary public repository containing source and releases.

## 1.3.1 — 2026-10-28

- Use the current ChatGPT Limit Panel name for the MCP server in plugin settings.

## 1.3.0 — 2026-10-28

- Add stable taskbar indicators and background refresh with the app window closed.
- Fix game/Start/Start11 visibility and transparent child composition.
- Add background transparency controls and complete current file branding.

## 1.1.0 — 2026-10-25

- Add **Check for updates** to the panel context menu and plugin control card.
- Compare the installed version with the latest stable GitHub release and offer
  **Open release** when a newer Windows build is available.
- Show distinct up-to-date, installed-ahead, timeout, connection and request-limit
  results in Russian and English, with light and dark native dialogs.
- Keep updates manual; no Git installation or GitHub sign-in is required.

## 1.0.1 — 2026-10-25

- Keep the panel visible when ChatGPT loses focus. Follow the target window's
  stacking order, including its normal/topmost band, without a foreign owner
  or input attachment. Other applications can cover both windows normally.
- Follow rapid window movement directly from native location events. A
  temporary 16 ms geometry timer fills event gaps while idle discovery stays
  at 200 ms. Native event hooks now subscribe only to the required event ranges.
- Defer preview rendering and diagnostic writes during movement, including
  incoming quota freshness updates. Flush the latest state when movement ends.
- Preserve hiding on minimize, close, cloaking and pause; retain no-activation
  behavior, menu layering, settings, themes, languages and autostart preference.
