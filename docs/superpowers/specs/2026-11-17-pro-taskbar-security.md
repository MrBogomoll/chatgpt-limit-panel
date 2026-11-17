# Pro display and taskbar credits

User-approved design and preview from 2026-11-15, resumed on 2026-11-17.

- Default quota visibility is automatic. Hide a confirmed absent five-hour limit. Offer both blocks (infinity for the confirmed absent limit) and weekly-only display.
- Missing, malformed or failed reads are unknown, never unlimited. Pro plus an explicitly null window and a valid weekly window establishes absence; a present short window takes precedence. Keep established layout through transient incomplete reads.
- The frame falls back to weekly when short is unlimited, regardless of visibility choice. The saved source preference remains intact for accounts that have a short limit.
- The taskbar indicator offers percentages or remaining Codex credits, with persisted settings and a single centered value for credits. Unknown credits show an em dash, unlimited credits show infinity, large balances use the existing compact formatter. Preserve drag, appearance, menu and no-activation behavior.
- Tray tooltip remains ChatGPT Limit Panel only. Dynamic tray rendering follows available quota rows.
- Do not copy Codex config or credentials into installer-generated sources. Test the stdout/stderr, normalized state and archive boundaries with synthetic secrets; audit existing owned artifacts without printing secret values.
- Base is stable v1.7.8. Local display/archive version 1.7.8_b.1; use compatible 1.7.8-b.1 for semver-only fields and 1.7.8.1 for assembly file version. No source push, tags or releases.
