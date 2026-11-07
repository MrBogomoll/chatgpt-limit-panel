# Plugin Uninstall Implementation Plan

> **For agentic workers:** Execute with superpowers:executing-plans in this session.

**Goal:** Replace manual Task Scheduler and folder cleanup with a confirmed uninstall launched from the plugin control card.
**Architecture:** A bundled native helper is copied to Temp before opening. It validates ownership and performs removal independently of the MCP process. The card exposes only the action that opens this confirmation window.
**Tech Stack:** C#/.NET Framework WinForms and existing task validation; Node.js MCP; HTML/JavaScript control card; native Codex plugin CLI.
**Spec:** ../specs/2026-11-07-plugin-uninstall-design.md

## Global Constraints

- No uninstall entry in the floating panel or taskbar/tray menus.
- Opening the helper and cancellation have no uninstall side effects.
- Settings removal requires explicit checkbox selection; credentials and unrelated plugins are excluded.
- Filesystem targets must be verified inside the intended cache, local-sources or panel-data roots; reject reparse points.
- All destructive verification uses temporary fixtures, not the live installation.

## Review Focus

- Invalid package roots, redirected directories and forged manifests must fail before deletion.
- Task replacement/running state must prevent deletion of unrelated Windows tasks.
- Native CLI failure and locked files must produce a partial/failure result.
- MCP shutdown must match executable and server arguments for this plugin only.
- Card localization, narrow layouts and repeated/cancelled opens must not start removal.

### Task 1: Native uninstall engine and confirmation window

- [x] Add failing native fixture tests for path ownership, settings choice, operation ordering, CLI failures and task safety.
- [x] Implement overlay/Uninstall.cs and build the independent GUI helper using existing Launcher/TaskHost utilities.
- [x] Prove fixture tests and render the confirmation window offscreen without activation.

### Task 2: Plugin entry and package integration

- [x] Add failing MCP/UI/controller tests: app-only open tool, empty arguments, copied helper, collapsed settings and complete translations.
- [x] Implement server/uninstall.mjs, the card action and package/build integration.
- [x] Run affected server, browser, packaging and native checks.

### Task 3: Delivery

- [x] Simplify bilingual uninstall instructions and explain optional data removal.
- [x] Build the review package, inspect previews and run the focused checks.
- [x] Review the complete change and report local/published status. Do not run the helper against the user's panel or publish a release before review.
