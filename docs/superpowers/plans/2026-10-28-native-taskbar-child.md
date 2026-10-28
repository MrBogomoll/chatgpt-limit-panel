# Native taskbar child implementation plan

> **For agentic workers:** Use subagent-driven-development or executing-plans task by task. Preserve the existing installed package until native tests pass.

**Goal:** Make Quota Panel follow the taskbar's native visibility and stacking, as the user's installed NetWorx does, without losing quota, preferences, input or collision protection.

**Architecture:** Attach only Quota Panel's own indicator HWND as a non-topmost child of the verified primary Shell_TrayWnd. Keep the current GUI process only when the parent's and child's DPI awareness contexts match exactly. The parent then controls game, Start, movement and clipping behavior. Keep measured allocation and a separately verified retention path; do not use foreground-game fullscreen or top-level overlay stacking to hide this child.

**Tech Stack:** Existing .NET Framework WPF, Win32 child-window APIs, existing Node host and Codex quota reader.

**Spec/evidence:** User explicitly requested NetWorx's stable behavior. Read-only native inspection proves NetWorx is a separate-process direct taskbar child, both PerMonitorV2. Isolated production-WPF child spike passed native attachment, movement, visibility, DPI and input checks. Its RenderTargetBitmap verifies the WPF visual, while real Explorer composition/context-menu acceptance remains required. Root dist artifacts networx-host-metadata-1793160824356.json and cross-child-report-20261028-041845-541.json.

## Global constraints

- Preserve RU/EN, automatic/manual themes, view, indicators, contour, side, transparency, quota and autostart.
- Change only own windows. Do not restart Explorer, games, NVIDIA or ChatGPT, resize their controls, inject code, register a deskband or alter system DPI awareness.
- Compare DPI contexts before SetParent. Reject incompatible/unknown hosts without resetting the GUI process's DPI context or falling back to a floating topmost window.
- Protect NetWorx, buttons, tray and other native/UIA controls. Child attachment does not itself reserve space.
- Exclude the verified own child subtree from occupancy. Never exempt foreign controls by class name alone.
- Keep the ready 1.2.0 and earlier beta archives immutable; target private 1.2.1-beta.3 after verification. No public stable publication and no physical Windows 10 claim.

## Review focus

- Parent recreation destroys the WPF child and permanently closes that instance; recreate a new indicator and its menu, without closing the main GUI/backend.
- A native child may retain IsWindowVisible while its parent is iconic; check the parent explicitly.
- Preserve alpha-one input at 100% background transparency, opaque text, nonactivation and context-menu operations.
- Unknown/stalled UIA may retain only an already verified footprint under current native layout evidence; fresh complete evidence must recover without a normal-desktop visit.
- Native child coordinates are parent-client coordinates; keep screen-coordinate allocation and DPI boundaries explicit.

## Tasks

### 1. Native attachment boundary

- [x] Write failing own offscreen cross-process parent tests for the production attachment helper.
- [x] Implement TaskbarAttachment.cs: own-HWND validation, exact host/DPI proof, child style/non-topmost attachment, physical-to-client mapping, checked placement and parent-generation/liveness checks.
- [x] Verify parent move/hide/show/minimize, no activation, mismatch rejection, failed attachment concealment and parent destruction. No real shell mutation in unit tests.

### 2. Child-aware allocation and evidence

- [x] Add child-mode native snapshot/safety/fingerprint entry points in TaskbarWindow.cs; preserve legacy/injected-fixture behavior.
- [x] Exclude only the verified own child native/UIA subtree; keep NetWorx and unknown controls protected.
- [x] In child mode use parent geometry/DPI/visibility instead of fullscreen memory and top-level overlap. Native parent order handles covering games and popups.
- [x] Test own-child exclusion, foreign widget collision, stale geometry, changed native controls and recovery from unknown snapshots.

### 3. Renderer and lifecycle integration

- [x] Prefer fresh valid allocation; use verified current footprint retention only when fresh acquisition is unavailable. Do not let an old menu rejection poison a fresh complete frame.
- [x] Bind and place at zero opacity before first pixels, then restore opacity after attachment/order/geometry proof. No independent TOPMOST path for native children.
- [x] Detect externally destroyed child HWND and recreate its TaskbarWindow/menu on the existing non-motion tracking tick. Dispose old menu watchers and preserve current rows/settings.
- [x] Test repeated game/Start-like parent transitions with own fake windows, native close/recreate, input and unchanged main-panel DPI/focus.

### 4. Private candidate and actual acceptance

- [x] Run native build, focused native regressions and the required combined suite.
- [ ] Bump coherent metadata to 1.2.1-beta.3, package to a new immutable folder, back up registered source/task, install via original source and CLI, compare all payload hashes and true physical preferences.
- [ ] Inspect real parent/child/non-topmost/DPI/geometry and visible pixels, verify ПКМ/transparency, then repeat actual Start/game/AltTab acceptance with the user.
- [ ] Record verified scope and remaining constraints; commit/push only private source. Do not mark the bug fixed merely because tests pass.
