param([string]$PluginRoot, [string]$OutputPath = (Join-Path (Get-Location) 'limit-panel-diagnostics.json'))
$ErrorActionPreference = 'Stop'
# Read-only environment metadata. No account, conversation or quota files are read.
Add-Type -TypeDefinition @'
using System;using System.Text;using System.Runtime.InteropServices;
public static class LimitPanelDiagnostics {
 public delegate bool Callback(IntPtr h,IntPtr p);
 [StructLayout(LayoutKind.Sequential)]public struct Rect {public int Left,Top,Right,Bottom;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr LoadLibrary(string n);
 [DllImport("kernel32.dll")]public static extern bool FreeLibrary(IntPtr h);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]public static extern IntPtr GetProcAddress(IntPtr h,string n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string c,string t);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr h);
 [DllImport("user32.dll")]public static extern bool AreDpiAwarenessContextsEqual(IntPtr a,IntPtr b);
 [DllImport("user32.dll")]public static extern bool EnumWindows(Callback c,IntPtr p);
 [DllImport("user32.dll")]public static extern bool EnumChildWindows(IntPtr h,Callback c,IntPtr p);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetClassName(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
}
'@
$diagOS = Get-CimInstance Win32_OperatingSystem
$diagFramework = Get-ItemProperty 'HKLM:/SOFTWARE/Microsoft/NET Framework Setup/NDP/v4/Full' -ErrorAction SilentlyContinue
$diagApis = [ordered]@{}
$diagLibrary = [LimitPanelDiagnostics]::LoadLibrary('user32.dll')
try { foreach ($diagName in @('GetDpiForWindow','GetWindowDpiAwarenessContext','AreDpiAwarenessContextsEqual','GetThreadDpiAwarenessContext')) { $diagApis[$diagName] = [LimitPanelDiagnostics]::GetProcAddress($diagLibrary,$diagName) -ne [IntPtr]::Zero } }
finally { if ($diagLibrary -ne [IntPtr]::Zero) { [void][LimitPanelDiagnostics]::FreeLibrary($diagLibrary) } }
$diagBar = [LimitPanelDiagnostics]::FindWindow('Shell_TrayWnd',$null)
$diagRect = New-Object LimitPanelDiagnostics+Rect
$diagBarInfo = [ordered]@{ found = $diagBar -ne [IntPtr]::Zero; rectangleCoordinateSpace = 'caller DPI context (may be virtualized)' }
$diagContext = [IntPtr]::Zero
if ($diagBarInfo.found) {
 [void][LimitPanelDiagnostics]::GetWindowRect($diagBar,[ref]$diagRect)
 $diagBarInfo.width = $diagRect.Right-$diagRect.Left; $diagBarInfo.height = $diagRect.Bottom-$diagRect.Top
 if ($diagApis.GetDpiForWindow) { $diagBarInfo.dpi = [LimitPanelDiagnostics]::GetDpiForWindow($diagBar) }
 if ($diagApis.GetWindowDpiAwarenessContext) { $diagContext = [LimitPanelDiagnostics]::GetWindowDpiAwarenessContext($diagBar); $diagBarInfo.dpiContext = $diagContext.ToInt64() }
}
$diagGuiProcesses = @(Get-Process -Name ChatGPTLimitPanel,QuotaOverlay -ErrorAction SilentlyContinue)
$diagGuiIds = @($diagGuiProcesses.Id)
$diagOwnWindows = [Collections.Generic.List[object]]::new()
$diagWindowCallback = [LimitPanelDiagnostics+Callback]{param($diagWindow,$diagUnused)
 $diagPid=[uint32]0; [void][LimitPanelDiagnostics]::GetWindowThreadProcessId($diagWindow,[ref]$diagPid)
 if ($diagGuiIds -contains $diagPid -and $diagApis.GetWindowDpiAwarenessContext) {
  $diagOwnContext=[LimitPanelDiagnostics]::GetWindowDpiAwarenessContext($diagWindow)
  $diagClass=[Text.StringBuilder]::new(256);[void][LimitPanelDiagnostics]::GetClassName($diagWindow,$diagClass,256)
  $diagOwnWindows.Add([ordered]@{class=$diagClass.ToString();visible=[LimitPanelDiagnostics]::IsWindowVisible($diagWindow);dpiContext=$diagOwnContext.ToInt64();sameDpiContextAsTaskbar=($diagApis.AreDpiAwarenessContextsEqual -and [LimitPanelDiagnostics]::AreDpiAwarenessContextsEqual($diagOwnContext,$diagContext))})
 }; return $true
}
[void][LimitPanelDiagnostics]::EnumWindows($diagWindowCallback,[IntPtr]::Zero)
if($diagBar -ne [IntPtr]::Zero){[void][LimitPanelDiagnostics]::EnumChildWindows($diagBar,$diagWindowCallback,[IntPtr]::Zero)}
$diagPayload = $null
if ($PluginRoot) {
 $diagResolved = [IO.Path]::GetFullPath($PluginRoot)
 $diagManifestPath = Join-Path $diagResolved '.codex-plugin/plugin.json'
 $diagManifest = if (Test-Path -LiteralPath $diagManifestPath) { Get-Content -LiteralPath $diagManifestPath -Raw | ConvertFrom-Json } else { $null }
 $diagPayload = [ordered]@{version=$diagManifest.version;guiPresent=(Test-Path -LiteralPath (Join-Path $diagResolved 'dist/overlay/ChatGPTLimitPanel.exe'));launcherPresent=(Test-Path -LiteralPath (Join-Path $diagResolved 'dist/overlay/ChatGPTLimitPanelLauncher.exe'));runtimePresent=(Test-Path -LiteralPath (Join-Path $diagResolved 'runtime/node.exe'))}
}
$diagTasks = @(Get-ScheduledTask -TaskName 'ChatGPTQuotaOverlay.Host.*' -ErrorAction SilentlyContinue)
$diagPackages = @(Get-AppxPackage '*OpenAI*' -ErrorAction SilentlyContinue | Select-Object Name,Version)
$diagResult = [ordered]@{schemaVersion=1;system=[ordered]@{name=$diagOS.Caption;version=$diagOS.Version;build=$diagOS.BuildNumber;architecture=$diagOS.OSArchitecture};frameworkRelease=$diagFramework.Release;requiredApis=$diagApis;taskbar=$diagBarInfo;guiRunning=($diagGuiProcesses.Count -gt 0);guiWindowContexts=$diagOwnWindows;packages=$diagPackages;codexOnPath=($null -ne (Get-Command codex -ErrorAction SilentlyContinue));plugin=$diagPayload;autostartTaskFound=($diagTasks.Count -gt 0);taskStates=@($diagTasks.State | ForEach-Object { $_.ToString() })}
$diagJson = $diagResult | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),$diagJson,[Text.UTF8Encoding]::new($false))
Write-Output $diagJson
