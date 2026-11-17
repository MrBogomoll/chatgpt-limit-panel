# Pro, taskbar credits and secret handling implementation plan

> Execute inline with superpowers:executing-plans. User approved the design and asked to proceed. Final independent review after implementation.

**Goal:** Adapt the existing panel to absent Pro short limits and optional taskbar credit display, and verify secrets do not cross output or packaging boundaries.
**Architecture:** Normalize explicit quota availability before formatting; preserve last confirmed availability during transient partial reads. Extend existing native layout with a short-block state and retain two logical quota slots. Reuse existing credit formatting for one centered taskbar value.
**Tech stack:** Node.js, C# WPF, existing native fixture harnesses.
**Spec:** ../specs/2026-11-17-pro-taskbar-security.md

## Global constraints

- Development version was 1.7.8_b.1 (semver 1.7.8-b.1, assembly 1.7.8.1). User explicitly authorized GitHub publication and agreed stable 1.8.0 on 2026-11-17.
- No live uninstaller tests or desktop takeover. Preserve user preferences when installing in background.
- Never output actual token/config values. Credentials remain owned by Codex.

## Review focus

- Missing windows, account plan changes, transient failures and stale data must not become false infinity.
- All layout modes and optional reset/credit blocks must fit, including moving/resizing and single-row tray/taskbar.
- Credit content choices must retain drag/menu/no-activation behavior and old preferences.
- Package and generated installer source must exclude config and authentication data; canary backend errors must not reach MCP or saved state.
- Beta updates/uninstall path validation must remain compatible without weakening ownership checks.

## Tasks

- [x] 1. Backend and security: add failing quota-availability and canary tests; implement normalization, transient-state retention, remove raw-config backup; verify relevant Node tests.
- [x] 2. Native UI: add failing native fixtures for visibility modes, contour fallback, taskbar credit value and persistence; implement layout/menu/render changes and translations; verify native fixtures and screenshots from isolated data.
- [x] 3. Delivery: synchronize beta metadata and packaging, focused verification and independent review; build/install locally, verify version/hash/settings/live state, report security findings and no publication.

## Verification and delivery

- Node focused suite: 89 passed; subsequent targeted Pro/installer tests passed after final enum-unknown and metadata-whitelist fixes.
- Native support: 5 passed, including 200 taskbar and 49 uninstall assertions on isolated fixtures. Full native presentation run covered 57 scenarios: only an obsolete menu-count assertion failed, corrected for the new Content submenu and rerun successfully. No production behavior was changed to satisfy the old expectation.
- Independent review found one transient `planType: unknown` layout reset; added a failing regression, normalized that enum value to unconfirmed, and verified the fix. No credential-output finding remained.
- Actual-account sanitized probe still reports Plus; real Pro live validation awaits an account transition. Pro, prolite and promax explicit-null quota snapshots are covered by fixtures.
- Inspected 15 existing local config backups: no nonempty credential-key assignments detected by the targeted audit. Existing live state had no credential fields; own process command lines had no credential-like arguments. No secret values were printed or saved by the audit.
- The local package whitelist and 23 text payload files were checked for private files and synthetic/recognizable key markers. Final ZIP SHA-256: c4dbd8b3547b41b45974b25b6fedc5dc6838abc95c0f3bcf2968c033ec453ea4.
- Installed version 1.7.8-b.1, displayed as 1.7.8_b.1, enabled and running; all three executable hashes match the package and existing preferences were preserved. No config backup was created. No publication was performed.
- Windows CFA blocked tools in OneDrive. Built in the isolated worktree outside OneDrive; protection settings were not changed. Current process lacks administrator rights to add exclusions.
- Native CLI replacement encountered locked old MCP runtime files. Restored five missing files from the verified prior package, stopped only old MCP processes using the reviewed executable/argv/creation-time/actual-CWD verification, and repeated the supported install successfully. No live uninstaller was executed; settings and unrelated processes were preserved.

## Stable release gate — 1.8.0

The user approved stable version 1.8.0 and installation of the same release build. Version metadata, native assembly, documentation and archive names use 1.8.0. Fresh focused verification: 89 Node tests and the native Pro presentation fixture passed. The final archive SHA-256 is 5b667a5c5c25a13a847a6c71a84e7f18cf2bf67001a24f2308c6f21d555faec0. Release publication must verify source main/tag, uploaded asset digests and the latest stable release. Real Pro account validation remains pending the user's plan transition; simulated Pro snapshots must not be described as live validation.
