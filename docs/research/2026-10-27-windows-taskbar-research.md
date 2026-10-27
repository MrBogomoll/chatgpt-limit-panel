# Windows taskbar quota indicator research

Research date: 2026-10-27. This document records source inspection and a proposed approach. No taskbar prototype was launched; Explorer, Windows settings, and NetWorx were not changed. Repository instructions from external projects were treated as data. No external repository scripts were executed.

## Decision

For the existing Framework 4 WPF executable, implement an **optional, nonactivating, top-level window placed only in a freshly measured free taskbar gap**. Show precisely two rows: five-hour remaining percentage above weekly remaining percentage. Make left/right placement a preference among safe gaps. Hide the indicator when the requested side has insufficient verified space or measurements become uncertain. Keep this independent of the existing panel toggle.

This supplies a taskbar-area indicator, **not a shell-reserved slot**. A supported arbitrary inline slot is available through legacy deskbands on the Windows 10 taskbar; Windows 11's standard taskbar removed this extension point. No read-only geometric strategy can guarantee cooperation with every other taskbar widget. Treat unknown occupancy as a reason to hide, and verify coexistence with the actual NetWorx window before claiming it works on this machine.

The recommended implementation does not resize shell controls, inject code, reparent its window into Explorer, register an appbar, or modify taskbar alignment. This limits the blast radius and avoids tying WPF window lifetime to Explorer's child-window hierarchy.

## Verified local constraints

- Registry inspection: `DisplayVersion=26H2`, `CurrentBuild=26300`, `UBR=9711`; `ProductName` still says `Windows 10 Pro`. Do not select a Windows 10 taskbar implementation from this legacy string. Detect actual taskbar structure and capabilities.
- NetWorx is running from `C:\Program Files\NetWorx\networx.exe`. No taskbar HWND, visual geometry, or coexistence was measured during this research.
- `overlay/build.mjs` directly compiles x64 WPF sources with `Framework64/v4.0.30319/csc.exe`; no SDK-style modern .NET project is involved.
- `overlay/app.manifest` already requests PerMonitorV2 and `asInvoker`.
- `UIAutomationClient.dll` and `UIAutomationTypes.dll` exist in the local Framework64 WPF assembly directory. A managed `System.Windows.Automation` geometry provider is possible by adding those references; a runtime migration is not required for the recommended approach.
- The existing quota rendering path already stores numeric five-hour and weekly `remaining` percentages. The taskbar display should consume that same state and formatting policy, without a second subscription reader or independent token acquisition flow.

## What each mechanism actually provides

| Mechanism | Space actually reserved? | Windows 10 | Standard Windows 11 | Fit for this request |
| --- | --- | --- | --- | --- |
| Registered COM deskband (`IDeskBand2`, `CATID_DeskBand`) | Shell hosts/sizes a band in the taskbar | Legacy supported route | Removed from the standard taskbar | Genuine inline layout on Windows 10, but requires an in-process COM shell extension and a separate deployment/lifecycle design |
| `Shell_NotifyIcon` | Shell allocates an icon cell | Supported | Supported | Appropriate for a tray icon; cannot provide an arbitrary-width two-row label, left/right placement, or guaranteed visible promotion |
| Appbar (`SHAppBarMessage`, `ABM_NEW` / `ABM_SETPOS`) | Reserves desktop work-area edge | Supported | Supported | Creates another screen-edge bar. It does not allocate an inline slot inside the existing taskbar |
| Taskbar button overlay/progress (`ITaskbarList3`) | Uses an application's existing button | Supported | Supported | Does not produce a two-row status label with independent placement |
| Top-level free-gap overlay | None | Technically possible | Technically possible | Best fit for current executable if conservative collision/visibility checks are accepted |
| Child HWND hosted under `Shell_TrayWnd` | None by `SetParent` alone | Technically possible | Used by third-party implementations | Looks embedded but uses private taskbar structure and adds Explorer/DPI lifecycle coupling; no universal widget coordination |
| Resizing taskbar app-button HWNDs | Manually changes foreign layout | Used by some legacy tools | Not a supported slot contract | Do not adopt: risks app-button clipping and conflicts with other tools |

Microsoft's Windows 11 specifications explicitly remove application customization of taskbar areas. Its band documentation describes shell-hosted COM bands and their in-process registration. Microsoft documents a separate appbar API and user-controlled notification icon visibility. These are distinct mechanisms, even when a project calls its feature a “taskbar widget.”

Sources: [Windows 11 changes](https://www.microsoft.com/en-us/windows/windows-11-specifications), [band objects](https://learn.microsoft.com/en-us/windows/win32/shell/band-objects), [taskbar extensions](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar-extensions), [appbars](https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars), [notification area](https://learn.microsoft.com/en-us/windows/win32/shell/notification-area).

## NetWorx: confirmed product behavior, unknown interoperability

[SoftPerfect KB 55](https://www.softperfect.com/contact/knowledgebase.php?article=55) says Windows 11 removed custom taskbar toolbars and that SoftPerfect implemented its own replacement. It does not publish a generic slot API or identify its HWND/placement algorithm. Inferring that NetWorx uses the same mechanism as any open-source project would be unsupported.

The [official changelog](https://www.softperfect.com/products/changelog.php?product_id=2) records left/right docking for a centered Windows 11 taskbar in 7.2.1, secondary-display selection in 7.3, and Explorer/startup recovery fixes in 7.3.1. This demonstrates that these details require dedicated implementation and testing; it does not establish compatibility with Quota Panel.

Coexistence must be established from visible window rectangles and the actual accessible taskbar content. A process name alone supplies no occupied rectangle. Do not relocate or reconfigure NetWorx, and do not assume a fixed-width empty region beside the tray.

## Deskband11Lib source findings

Inspected repository: [airtaxi/Deskband11Lib](https://github.com/airtaxi/Deskband11Lib), commit `33ed5c38ab1d9bb3ab0b9e7d68126d6449fdf69b`.

- [Core project](https://github.com/airtaxi/Deskband11Lib/blob/33ed5c38ab1d9bb3ab0b9e7d68126d6449fdf69b/Deskband11Lib.Core/Deskband11Lib.Core.csproj#L3) and [WPF project](https://github.com/airtaxi/Deskband11Lib/blob/33ed5c38ab1d9bb3ab0b9e7d68126d6449fdf69b/Deskband11Lib.Wpf/Deskband11Lib.Wpf.csproj#L3) are version 1.4.2, target `net10.0-windows10.0.26100.0`, and use modern SDK/build tooling. The current NuGet assemblies cannot be referenced by Quota Panel's Framework 4 executable.
- [Host implementation](https://github.com/airtaxi/Deskband11Lib/blob/33ed5c38ab1d9bb3ab0b9e7d68126d6449fdf69b/Deskband11Lib.Core/TaskbarContentHostBase.cs#L211) calls `SetParent` to make its own window a taskbar child, changes styles to `WS_CHILD`, and positions/clips its own HWND. This is window hosting, not deskband registration or native space reservation.
- [Layout calculation](https://github.com/airtaxi/Deskband11Lib/blob/33ed5c38ab1d9bb3ab0b9e7d68126d6449fdf69b/Deskband11Lib.Core/Internal/TaskbarLayoutCalculator.cs#L182) uses gaps between Widgets/Start and between app buttons/notification area. On a left-aligned taskbar, its left-placement modes resolve to the right gap. Insufficient space can shrink a label, so Quota Panel needs an explicit minimum-text-size check rather than accepting any positive width.
- [UI Automation reader](https://github.com/airtaxi/Deskband11Lib/blob/33ed5c38ab1d9bb3ab0b9e7d68126d6449fdf69b/Deskband11Lib.Core/Internal/TaskbarButtonReader.cs#L112) enumerates taskbar descendants, recognizes `StartButton`, `WidgetsButton`, `SystemTrayIcon` and `NotifyItemIcon`, and groups visible Explorer-process buttons. It runs the COM work off the UI thread. Foreign-process widget HWNDs are not a general obstacle list. A failed measurement keeps the old cache; that is unsuitable for a strict fail-closed design without a freshness deadline.
- [Slot registry](https://github.com/airtaxi/Deskband11Lib/blob/33ed5c38ab1d9bb3ab0b9e7d68126d6449fdf69b/Deskband11Lib.Core/Internal/SlotRegistryStore.cs#L10) is a library-specific named memory-mapped file and mutex: `Local\Deskband11Lib.SlotRegistry`. It coordinates only participating hosts, not arbitrary widgets such as NetWorx.
- [Window locator](https://github.com/airtaxi/Deskband11Lib/blob/33ed5c38ab1d9bb3ab0b9e7d68126d6449fdf69b/Deskband11Lib.Core/Internal/TaskbarWindowLocator.cs#L48) locates `Shell_TrayWnd` / `Shell_SecondaryTrayWnd`; secondary identities are based on X-sorted windows, not persistent display device identities. Do not reuse this indexing as a saved monitor identity.
- [Lifecycle service](https://github.com/airtaxi/Deskband11Lib/blob/33ed5c38ab1d9bb3ab0b9e7d68126d6449fdf69b/Deskband11Lib.Core/Internal/TaskbarWindowMonitorService.cs) polls taskbar handles/DPI. The host raises events requiring the app to recreate its WPF window after Explorer restart, display disappearance, or DPI changes. No general fullscreen/autohide safety policy was established by the inspected implementation.
- [License](https://github.com/airtaxi/Deskband11Lib/blob/33ed5c38ab1d9bb3ab0b9e7d68126d6449fdf69b/LICENSE): MIT, copyright airtaxi 2026. Any copied substantial code requires preserving the notice/license. The runtime dependency and behavior still make wholesale adoption a poor fit here.

## TrafficMonitor source findings

Inspected repository: [zhongyang219/TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor), commit `930f17533d6098989ebad62f210aa97d75ef174b`.

- [Shared taskbar dialog](https://github.com/zhongyang219/TrafficMonitor/blob/930f17533d6098989ebad62f210aa97d75ef174b/TrafficMonitor/TaskBarDlg.cpp#L1016) calls `SetParent` on its own window. Its failure path can position a top-level window instead. It is not a registered deskband.
- [Classic taskbar implementation](https://github.com/zhongyang219/TrafficMonitor/blob/930f17533d6098989ebad62f210aa97d75ef174b/TrafficMonitor/ClassicalTaskbarDlg.cpp#L14) finds ReBar/MSTask windows and shrinks/moves the shell's app-button region to make room, restoring it on shutdown. This is manual shell layout modification, not a supported cross-widget slot allocator.
- [Windows 11 implementation](https://github.com/zhongyang219/TrafficMonitor/blob/930f17533d6098989ebad62f210aa97d75ef174b/TrafficMonitor/Win11TaskbarDlg.cpp#L5) measures tray and Start rectangles and positions its own child window beside them. `ResetTaskbarPos` is empty in this path. Secondary taskbars lacking `TrayNotifyWnd` use a fixed scaled 88-pixel clock allowance; missing primary geometry uses a configured right margin. The source does not establish that app buttons or unrelated widgets can never overlap. Left selection is overridden to right for left-aligned Windows 11 taskbars.
- [License](https://github.com/zhongyang219/TrafficMonitor/blob/930f17533d6098989ebad62f210aa97d75ef174b/LICENSE) is **Anti 996 v1.0 (Draft)**, not MIT. Treat this as a mechanics reference; do not silently transplant its implementation into Quota Panel under a different license.

## codex-usage-widget source findings

Inspected repository: [ognjeeen/codex-usage-widget](https://github.com/ognjeeen/codex-usage-widget), commit `940d722d4044242bbe73f8056014449c2ef827a7`.

- [Interop implementation](https://github.com/ognjeeen/codex-usage-widget/blob/940d722d4044242bbe73f8056014449c2ef827a7/src/CodexUsageWidget/Infrastructure/Windows/TaskbarWindowInterop.cs) uses `WS_EX_TOOLWINDOW` / `WS_EX_NOACTIVATE` and `HWND_TOPMOST`, measures only primary `Shell_TrayWnd`, and places the label immediately before `TrayNotifyWnd`; missing tray geometry falls back to a fixed 240 physical-pixel allowance. It sets `GWLP_HWNDPARENT` to make the taskbar the **owner** of its top-level window. This is different from child-window `SetParent`, as [Microsoft documents](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw).
- [Label window](https://github.com/ognjeeen/codex-usage-widget/blob/940d722d4044242bbe73f8056014449c2ef827a7/src/CodexUsageWidget/Views/TaskbarLabelWindow.xaml.cs#L213) repositions every two seconds plus window-change callbacks and hides for a foreground fullscreen window. Its [XAML](https://github.com/ognjeeen/codex-usage-widget/blob/940d722d4044242bbe73f8056014449c2ef827a7/src/CodexUsageWidget/Views/TaskbarLabelWindow.xaml#L6) specifies 102×40 logical pixels and one visible percentage. There is no app-button/NetWorx collision probe, left-placement selector, monitor selector, or explicit autohide check in this path.
- [Fullscreen detector](https://github.com/ognjeeen/codex-usage-widget/blob/940d722d4044242bbe73f8056014449c2ef827a7/src/CodexUsageWidget/Infrastructure/Windows/FullscreenWindowDetector.cs) compares foreground-window DWM bounds against the reference monitor bounds. Its P/Invoke strategy can be implemented on Framework 4, replacing modern C# and runtime APIs where needed.
- [Project](https://github.com/ognjeeen/codex-usage-widget/blob/940d722d4044242bbe73f8056014449c2ef827a7/src/CodexUsageWidget/CodexUsageWidget.csproj#L5) targets .NET 10. The [MIT license](https://github.com/ognjeeen/codex-usage-widget/blob/940d722d4044242bbe73f8056014449c2ef827a7/LICENSE) permits reuse with attribution; the app cannot simply be added as a Framework 4 dependency. Its existing placement algorithm does not meet this task's collision constraints.

## Concrete geometry-provider design for Framework 4

This is a proposal, not an implemented or validated provider.

1. Enumerate real taskbar HWNDs and associate them with monitors through `MonitorFromWindow` / `GetMonitorInfo`. Keep native geometry in physical screen pixels, including negative coordinates. Persist monitor device identity, not screen index. Support one selected monitor first unless “all monitors” is explicitly part of the UI.
2. Use an independent WPF indicator window with no shell owner or parent, `ShowActivated=false`, `ShowInTaskbar=false`, `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`, and `SWP_NOACTIVATE`. Reuse the panel's existing click/focus conventions. A nonactivating click must consume the indicator click rather than falling through onto an app button. Do not set `WS_EX_TRANSPARENT` if the indicator is interactive.
3. Acquire taskbar accessibility geometry on one dedicated background worker. With managed Framework 4 APIs, reference `UIAutomationClient` and `UIAutomationTypes`, call `AutomationElement.FromHandle(taskbarHwnd)`, and cache a bounded snapshot of bounding rectangles, visibility, AutomationId, process ID, control type, native handle and hierarchy. The [Microsoft threading guidance](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-threading-issues) requires separating desktop UIA calls from the app's UI thread. [CacheRequest](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/use-caching-in-ui-automation) reduces repeated cross-process property reads.
4. Use the accessibility tree and native child-window rectangles together. Protect Start, Search, Task View, pinned/running app buttons, Widgets/weather, overflow, language/input indicators, tray/quick settings, clock, notification controls and Show Desktop. Recognize structural shell containers rather than treating their full rectangles as free or occupied. Unlike Deskband11Lib's button filter, include third-party visible HWNDs regardless of process ID. Also inspect visible top-level windows intersecting the taskbar, excluding only well-understood shell/background/own windows. Unknown or unbounded visible content means occupancy is uncertain.
5. Intersect obstacle rectangles with the taskbar row, inflate by a small DPI-scaled margin, union them into protected horizontal intervals, and subtract from the row. Choose a sufficiently wide gap on the preferred side. Measure the widest possible percentage text, including `100%`, for both rows using the real font and DPI. Do not squeeze text below the accepted size. If the requested side has no suitable gap, hide rather than cover controls or silently move to a different side.
6. Mark snapshots with taskbar HWND, Explorer PID, monitor identity, DPI, acquisition generation and timestamp. Discard measurements after shell recreation, display/DPI/layout changes, timeout, exception or a short freshness deadline. No valid snapshot means hide. Do not retain old successful geometry indefinitely. Read-only UIA still needs failure handling: a `Task` timeout does not cancel a hung unmanaged call. Limit work to one in-flight probe; a small separate geometry helper process is the stronger option if bounded shutdown/recovery is required. [IUIAutomation2](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nn-uiautomationclient-iuiautomation2) additionally exposes native connection/transaction timeouts, which are not equivalent to cancelling arbitrary managed tasks.
7. Recheck before showing or moving and refresh on shell/window/location/display changes, backed by a modest timer. Hide promptly on uncertainty; debounce showing until geometry is stable. Event callbacks are invalidation signals, not permission to reuse a stale layout.

UIA cannot establish absence of every conceivable owner-drawn widget. Native child-HWND detection improves NetWorx-style coexistence but does not prove coverage of drawing injected inside a shell surface. A requirement of absolute non-overlap with all possible widgets is unachievable through this approach. Keep the setting optional and describe the supported/verified environments precisely.

## Visibility and lifecycle policy

| Condition | Recommended behavior | Reason / evidence |
| --- | --- | --- |
| Auto-hide enabled, taskbar fully revealed | Permit only while full-width row is completely on-screen and readable | Enablement is distinct from hidden state. A sliding, offscreen, or two-pixel strip fails geometry validation. Do not reveal or hold open the bar programmatically. |
| Foreground fullscreen on the chosen monitor | Hide immediately | Compare foreground extended-frame bounds to that monitor's full bounds; exclude shell/own windows and ordinary maximized framed windows. Maximized apps can fill monitor bounds when auto-hide is enabled. Borderless fullscreen remains protected. |
| Explorer disappears or taskbar handle changes | Hide, discard geometry, reacquire | The top-level indicator survives, but all old shell handles/snapshots are invalid. `TaskbarCreated` is a useful invalidation message, supplemented with handle checks. |
| DPI changes | Hide and rebuild layout using current taskbar DPI | Keep physical and logical units separate; no fixed 88/240-pixel clock fallback. |
| Selected display disconnects | Hide until a supported selected taskbar returns | Do not silently move onto a different display and possibly cover controls. |
| Tray expands, app buttons grow, NetWorx moves, text labels enabled | Invalidate, remeasure, choose safe gap or hide | Every prior empty gap can become occupied. |
| Missing Start/tray/app-group evidence or accessibility failure | Hide | An empty result is not evidence of an empty taskbar. |
| Unsupported orientation/taskbar replacement | Hide with a diagnostic reason | Legacy vertical/multi-row/replacement taskbars need explicit provider support and separate acceptance. |
| User disables indicator | Hide and stop its probe/timer/hooks | Existing floating panel and settings retain their behavior. |

[SHAppBarMessage](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shappbarmessage) documents `ABM_GETSTATE`, taskbar bounds, and per-monitor auto-hide queries. These queries do not register a bar. [Microsoft's taskbar documentation](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar#taskbar-creation-notification) describes `TaskbarCreated`. [SHQueryUserNotificationState](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shqueryusernotificationstate) is designed for notification appropriateness and has no fullscreen transition notification, so it is insufficient as the sole fullscreen detector.

Microsoft also documents [cross-process `SetParent` DPI caveats](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent): parent/child DPI-awareness mismatch can force a child-process awareness reset. This reinforces keeping the proposed overlay independent from Explorer rather than adopting hosting solely to imitate a native band.

## Validation required before a compatibility claim

The research did not run these checks. Automated geometry tests and actual desktop acceptance serve different purposes.

- Geometry tests: negative monitor coordinates, all supported scales, fractional scaling rounding, 0/100/unavailable values, full/fragmented/unknown occupied intervals, left versus right, stale generations, shell HWND reuse, invalid DPI/bounds, no fitting two-row height, foreign child windows, and acquisition failures.
- Actual Windows acceptance: left/right on this 26300.9711 machine with NetWorx visible; Start/Search/Task View/app buttons/Widgets/tray/clock all clickable and unobscured; many running apps and taskbar button labels; light/dark; 100/125/150/200% where available; movement and resizing of NetWorx; changing tray width and display scaling.
- Lifecycle acceptance: secondary taskbars and monitor hot-plug if available, auto-hide and reveal, borderless/exclusive fullscreen on each relevant monitor, Explorer restart **only during an explicitly authorized test**, lock/unlock and sleep/resume, enable/disable and clean shutdown. This research did not restart Explorer.
- Focus acceptance: clicking indicator and its menu must not activate or click through into an underlying app; ordinary desktop work must keep foreground focus.
- Failure acceptance: blocked/missing provider, unknown widget geometry, insufficient gap, and stale snapshot must result in a hidden indicator with a useful local diagnostic, not a guessed fixed offset.

Until that desktop acceptance is observed, report “implementation/automated checks passed; desktop/NetWorx coexistence not yet verified” rather than “works with all taskbar widgets.”

## Implementation handoff in this branch

The initial implementation now lives in `overlay/TaskbarLayout.cs` and `overlay/TaskbarWindow.cs`. Its supported geometry is the primary, horizontal, bottom taskbar. The allocator uses a 48×32 DIP label, four-DIP obstacle clearance, and the requested taskbar half. Unknown occupancy, insufficient room, hidden/autohidden/fullscreen state, unsupported geometry and provider errors hide the label.

The production source scans UIA on one background worker, accepts at most 1.6-second-old geometry, and rejects scans taking over 1.2 seconds. A stuck unmanaged scan is not forcibly cancelled; there is at most one in-flight scan and the visible label fails closed when its cache expires. Every cached read additionally makes fast native handle/bounds/DPI/revealed-row/fullscreen checks. A fully revealed auto-hide taskbar is eligible; its hidden and intermediate animation geometry is rejected. Ordinary maximized framed apps are distinguished from borderless fullscreen. Own process top-level windows, including its menus, are excluded from foreign obstacles. The indicator preserves Z order while its context menu or the integration-provided `IsOtherMenuOpen` callback indicates another own menu is open. Top-level foreign windows are considered only above the taskbar in Z order; visible background windows beneath the taskbar do not occupy a gap. Foreign child widget rectangles remain protected. Unknown native leaf HWNDs are also protected when hosted inside Explorer, covering classic painted deskbands that expose only a UIA Pane; only a small explicit shell-layout class allowlist is exempt, and that exemption does not apply to foreign-process children.

The UIA root is obtained before activating a cache-only request, preserving its live provider handle for `FindAll`. Returned descendants use cached properties. Keeping the root itself cache-only caused a concrete `SafeHandle` failure during the coordinating agent's read-only probe and was corrected. An isolated own-window UIA regression covers the corrected acquisition pattern.

`tests/overlay-taskbar.test.mjs` compiles the actual Framework 4 classes and runs `tests/overlay-taskbar.cs`. The focused run passed 120 assertions for gap allocation, DPI/negative coordinates, protected regions, uncertainty/fullscreen/autohide flags, revealed versus sliding auto-hide geometry, maximized framed versus borderless fullscreen classification, native painted-leaf classification, a bounded background worker/cache, two visible percentages, dim stale values, no foreign owner, nonactivation, own and other-menu Z-order preservation, above/below-boundary native window enumeration, and disposal. All displayed test windows are owned by the isolated test process and positioned offscreen; the UIA test accesses only those own controls. No Explorer restart or foreign setting/window mutation was used.

This evidence does not establish Windows 10 acceptance, secondary-monitor support, arbitrary widget interoperability, or a complete interactive acceptance of the installed app. The coordinating agent owns integration, full regressions, and any explicitly authorized desktop acceptance.
