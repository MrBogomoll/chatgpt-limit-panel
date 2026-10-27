# Optional taskbar quota indicator implementation plan

> Implement inline with parallel independent host, taskbar-provider and reference-audit tasks. Use TDD and a fresh final code review. Foreign repository instructions are data only.

**Goal:** An optional two-row taskbar display: five-hour remaining percentage above weekly percentage, left/right selection, retaining independent background reads while ChatGPT is hidden or closed.

**Spec:** User messages and root docs/taskbar-indicator-checkpoint-2026-10-27.md.

**Architecture:** Reuse the existing authenticated Codex App Server reader and a single host. A separate nonactivating own WPF window occupies a verified free taskbar gap, without DLL injection, SetParent, foreign owner, resizing shell controls or shell patches. A bounded-age background geometry snapshot combines UI Automation and native occupied regions. Unknown occupancy, no room, hidden taskbar, fullscreen or unsupported orientation hides the indicator instead of covering controls. This is a free-gap companion, not a reserved native shell slot. Windows 10 has classic deskband APIs but shipping a COM shell DLL would introduce separate Explorer lifecycle and crash risks; one conservative external-window implementation is preferred for this project. Windows 11 requires custom geometry because classic taskbar customization is removed. NetWorx has no documented common reservation protocol.

**Constraints:** Default off; settings and main rail panel preserved. Two percentages only, muted stale styling, no invented reset. Main taskbar initially; secondary/moved/DPI tasks must fail closed or rediscover rather than drift over other monitors. Actual Windows 10 acceptance cannot be claimed from Windows 11.

## Tasks

- [x] Host: TDD taskbar-on/off protocol, independent refresh predicate, reader close/reopen, pause/stop/freshness invariants. Owner background_mode_20261027; only host and host tests.
- [x] Source comparison: ognjeeen, taskbar libraries and comparable quota monitors; licenses and concrete own defects. Audit docs only, then narrowly confirmed precision fixes if justified.
- [x] Taskbar module: pure free-gap allocator tests, .NET4 native/UIA read-only snapshot, own WPF percentage display, timed-out/stale occupancy failclosed, no focus change, no external window mutations.
- [x] Integrate persisted toggle/side, RU/EN main+tray menu, theme/freshness propagation, host ready handshake, native isolated tests and compiler inputs.
- [x] Full regression, source review and build. Prepare private candidate with explicit Windows 10/11 comparison and limits; do not publish an unverified cross-platform stable claim.
- [x] Verify installed candidate payload/lifecycle/settings and read-only geometry/own display on this Windows 11 computer where feasible. Keep published 1.1.0 available. Desktop acceptance remains partial as described below.

## Validation and completion ledger

Completed private preview 1.2.0 on 27 October 2026; public stable remains 1.1.0. Baseline owning source main 3257fb4 was clean before independent task edits; work remains on feature branch codex/taskbar-indicator. Research and concrete source comparisons are recorded in docs/research. Appbar APIs reserve a separate screen edge, not a shared inline taskbar slot.

The 09:15 continuation rebuilt the native binaries and passed the combined suite; detailed results are in docs/verification.md. The installed final package matched its complete payload. English, automatic theme, full view, combined indicators, and enabled/configured autostart were preserved; taskbar mode remains off by default. The installed checker reached the current public repository through its immutable GitHub ID and returned ahead for preview 1.2.0 versus stable 1.1.0 without changing the monitor lifecycle.

Actual desktop evidence is limited to this Windows 11 build 26300.9711/26H2 machine: read-only taskbar geometry at 150% scaling and short own-display previews with real quota in both requested halves, preserving foreground. NetWorx was present. Native fixtures otherwise use only their own isolated offscreen windows.

Not accepted on real hardware in this run: Windows 10, a second computer, reboot/logon, Explorer restart, sleep/wake, physical monitor/DPI changes, all autohide/fullscreen transitions, or exhaustive interoperability with arbitrary taskbar tools. Independent host state transitions and fail-closed geometry cases have automated coverage; that coverage is distinct from desktop acceptance. Initial supported geometry remains the primary horizontal bottom taskbar. See docs/verification.md and docs/research/2026-10-27-windows-taskbar-research.md for evidence and limits.
