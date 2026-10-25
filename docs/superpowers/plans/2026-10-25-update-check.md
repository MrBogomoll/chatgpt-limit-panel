# Quota Panel 1.1.0 update check implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task by task. Review using requesting-code-review before release.

**Goal:** Add the previously approved manual update check in the native menu and plugin card.

**Architecture:** One reusable Node update checker reads the public GitHub latest stable release, validates its version and origin, and reports available/current/ahead/error. Both MCP and the overlay host use it; the native UI and card localize the structured result. Opening the release requires a separate click.

**Tech Stack:** Existing Node 24, C# WPF, MCP App HTML/JavaScript; no new runtime dependencies.

**Spec:** docs/ROADMAP.md and the user's instruction to build 1.1 now.

## Global constraints

- Public repository: MrBogomoll/quota-panel-releases. No GitHub token or Git required.
- Manual checks only; no automatic installation or task migration feature.
- Stable semantic versions only; reject drafts, prereleases, invalid responses and external release URLs.
- Bounded network timeout, coalesced duplicate clicks and short request cooldown; network errors must never mean up to date.
- Russian and English, current light/dark themes and existing settings preserved.
- Release notes follow docs/RELEASE_NOTES.md; validation belongs in private docs.

## Review focus

- Numeric versions 1.10.0 vs 1.9.0 and installed-ahead state.
- Offline, timeout, rate limit, malformed data and unpublished releases.
- Arbitrary server URLs never become executable commands or external links.
- Repeated checks and host shutdown do not duplicate requests or reopen dialogs.
- Freshness updates, card status polling and language changes preserve update results.

### Task 1: Checker and protocols

Files: new plugins/quota-panel/server/updates.mjs and tests/quota-panel-updates.test.mjs; modify server/index.mjs, server/mcp.mjs, server/language.mjs, overlay/host.mjs and host/MCP tests.

- [x] Write failing checker cases for versions, result validation, network failures, timeout and coalescing.
- [x] Implement checker and safe release URL normalization; use the same result in host and MCP.
- [x] Add native check-updates command handling and check_quota_panel_updates tool, independent of monitor lifecycle.

### Task 2: Native and card controls

Files: overlay/Overlay.cs, overlay/Language.cs, plugin ui/control.html and control.mjs, native and browser tests.

- [x] Add failing UI cases for manual request, busy state, results, language, safe explicit release opening and no quota mutation.
- [x] Implement context-menu action and themed result dialog; defer all network work to the host.
- [x] Add card action and separate persistent result/link; use host open-link capability.
- [x] Verify both languages, both themes and narrow card layout using own isolated fixtures.

### Task 3: Release and installation

Files: versioned manifests/package/tests, overlay/package-plugin.mjs, install-plugin.mjs, release docs and README.

- [x] Bump to 1.1.0 and include the shared checker in both installed import locations.
- [x] Run relevant suite, review, build and package; preserve prior archives.
- [x] Migrate this user's owned startup task from 1.0.1 to 1.1.0 with backup, reload registered source, verify payloads and live update response.
- [ ] Publish private source and public release with user-facing RU/EN notes, then verify anonymous download and latest-release response.
