# Architecture

- plugins/chatgpt-limit-panel/: plugin manifests, MCP control server, interface, icons and the read-only Codex limit reader.
- overlay/host.mjs: owns the native GUI process, polls the account limit reader and forwards current/stale state. Taskbar mode keeps polling independent of the main ChatGPT window.
- overlay/Overlay.cs: WPF sidebar view, presentation modes, theme/language and menu controls.
- overlay/TaskbarWindow.cs, TaskbarAttachment.cs and TaskbarChildSurface.cs: allocate a verified free taskbar gap, create a layered native child and preserve nonactivating input.
- overlay/Launcher.cs and TaskHost.cs: cooperative start/stop and a per-user Windows logon task.
- overlay/Preferences.cs: persisted preferences and coordinated writes.
- server/updates.mjs: an explicit GitHub release check using a stable repository ID; validates owner, release and asset links. It never installs an update.
- tests/: backend, protocol, UI, native placement and packaging regression checks.
- tools/: reproducible documentation rendering from production controls with demo data.

The program asks the existing signed-in local Codex App Server for account/rateLimits/read. It does not scrape the native limit popup or fabricate reset dates. Unknown and stale values remain distinguishable. GitHub requests happen only when checking releases.

Persistent installation identifiers, IPC names and the preferences directory are compatibility boundaries. Display/file branding can evolve independently; changing persistence identifiers requires a deliberate migration. The installed runtime and published release archives are separate from repository documentation.
