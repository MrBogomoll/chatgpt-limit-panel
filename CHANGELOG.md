# Changes

## 1.7.3 — 2026-11-07

- Restore the readable Credits label in full and compact views; keep only the balance in ultra.
- Reserve a 3 px inset around credit content and at least 4 px between quota bars and the row divider.
- Align the last visible content 8 px above the bottom edge in all three modes, including stale states and optional content combinations.
- Give compact content sufficient height without shrinking quota text. Remove invisible stale labels from fresh full-mode layout.

## 1.7.2 — 2026-11-07

- Use the same 4 px quota bar thickness in full, compact and ultra modes.
- Use a simple 14 px C mark for credits in full and compact modes; ultra shows only the balance and reserves the full content width for large values.
- Replace left/right placement choices with Reset position for the draggable taskbar indicator, consistently in panel, tray and indicator menus. Remove the redundant status line.
- Preserve saved manual placement until an explicit reset; reset restores automatic default placement without disabling percentages.

## 1.7.1 — 2026-11-07

- Replace the abbreviated credits label with a monochrome vector coin, alongside the balance in ultra mode and above it in larger modes. Preserve accessible names and exact balance tooltips.
- Group panel settings into Content, Appearance, Placement and Settings; keep refresh, reset details, update checks, pause and stop directly accessible. Group content and placement consistently in the tray menu.
- Rename panel size and data refresh controls to make their purpose clearer.

## 1.7.0 — 2026-11-07

- Default fresh installations to bars and outline while preserving saved indicator choices.
- Add an optional reset block above quotas: count/date/time in full mode, count/date in compact, count only in ultra; persist visibility in panel and tray menus.
- List individual available reset expirations in a read-only submenu, ordered by expiry, with orange warnings at 10 days or less and red at 3 days or less. Mark unknown, incomplete and stale data explicitly.
- Add an independently optional Codex credits block between resets and quotas in all three modes, with precise balance tooltips. API billing balance is not supplied by this data source.
- Use one lighter 2 px divider before main quotas in full and compact modes; keep ultra and internal separators thin. Account for both optional blocks in safe placement.

## 1.6.1 — 2026-10-30

- Automatically relocate a stopped, verified launch task when the plugin installation path changes, preserving task and autostart settings.
- Recognize supported previous package paths without requiring the old files, while rejecting unrelated, running or concurrently replaced tasks.
- Simplify the bilingual upgrade guide to stop, replace ZIP and start.

## 1.6.0 — 2026-10-30

- Add optional unpinned placement inside the ChatGPT window, preserving bounds, saved positions and sidebar repinning.
- Keep unpinning and position reset beside the sidebar instead of jumping to the centre.
- Add a separate sidebar background opacity slider, independent of taskbar transparency and preserving opaque content.

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
