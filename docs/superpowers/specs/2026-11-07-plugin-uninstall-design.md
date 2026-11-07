# Plugin uninstall from the control card

Approved scope: the user wants fewer manual full-uninstall steps and explicitly chose the plugin's own controls, not the panel's right-click menu. This design records that choice (Codex, 2026-11-07).

- Entry: collapsed Plugin settings in the existing control card, with Remove plugin… separate from start/stop. It opens a native confirmation window; opening it changes no installation state.
- The native helper runs from a temporary copy outside the package so it can complete after its MCP server and package are removed. One confirmation starts removal. Removing panel settings, positions and logs is optional and unchecked by default.
- Sequence: validate installed cache root and manifest; discover the local Codex CLI; validate the owned Windows task; request cooperative monitor shutdown; remove the validated stopped task; close only this plugin's MCP processes; invoke native plugin removal; verify removal; clean verified generated local sources. Delete settings only when explicitly selected.
- Preserve ChatGPT/Codex sign-in, unrelated plugins, downloaded ZIPs and project checkouts. Reject redirected paths and unsafe or changed ownership evidence. Report incomplete cleanup rather than claiming success.
- Test only isolated fixtures. Do not uninstall the user's live panel. Build a local review package and show the control-card/native-window previews before publication.

Approaches considered: a standalone ZIP uninstaller and a panel context-menu command. The user selected the plugin controls to avoid accidental clicks on the panel. The same detached helper provides reliable removal without depending on a running card.
