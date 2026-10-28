# Windows compatibility investigation

The user reports that the panel does not work on Windows 10. The failure stage
has not been identified and no Windows 10 computer is available for direct testing.
Windows 11 results must not be presented as Windows 10 validation.

## Current evidence and candidates

- Vertical Start11 placement was reproduced on the available Windows 11 system.
  A redundant classic layout peer masked free space; this is a distinct measured
  issue and does not establish the cause of the Windows 10 report.
- The native child path requires the widget and host to have matching DPI contexts.
  A mismatch produces `dpi-context-mismatch` before displaying the child. Explorer's
  context on the affected Windows 10 machine must be measured, not assumed.
  Microsoft documents DPI mismatch behavior for cross-process
  [SetParent](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent).
- The native DPI APIs require Windows 10 version 1607 or later:
  [GetDpiForWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow)
  and [GetWindowDpiAwarenessContext](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowdpiawarenesscontext).
  This requirement alone does not explain a failure on a newer Windows 10 build.
- Startup, local Codex sign-in, the packaged runtime, task configuration, sidebar
  discovery, and taskbar allocation are separate stages. Determine whether the
  process never starts, starts without fresh limits, or starts but cannot attach.

## Read-only report for a Windows 10 session

Run `tools/diagnose-windows.ps1` from a saved copy of this repository. Optionally
pass `-PluginRoot` pointing to the installed plugin folder and `-OutputPath` for
the JSON report. It reads OS/framework versions, required API availability,
taskbar and running GUI DPI contexts, package metadata and owned task states.
It does not change tasks/settings, run the plugin, read credentials or conversations,
or request account limits. It does not resolve the compatibility issue by itself.

After obtaining the report, reproduce the failing stage on Windows 10 and verify
the proposed fix there. Access to that machine is currently inconvenient, so this
investigation must not block the already available placement work.
