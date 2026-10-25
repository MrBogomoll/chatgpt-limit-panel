# Changes

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

Validation: 116 automated checks passed, including independent test-process
WinEvent delivery and isolated native focus/Z-order tests. The installed MCP
and native monitor passed lifecycle, real-quota and transport-close checks.
The user confirmed both focus-loss visibility and rapid dragging on ChatGPT.
Physical Windows reboot and installation on a second computer were not run.
