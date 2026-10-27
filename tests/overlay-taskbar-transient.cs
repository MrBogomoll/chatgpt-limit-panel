using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QuotaOverlay;

internal static class TaskbarTransientTest {
  static int assertions;
  static void Check(bool condition,string message) {assertions++;if(!condition)throw new Exception(message);}
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  static void FullscreenTransitions() {
    var memory=new TaskbarFullscreenMemory();IntPtr primary=new IntPtr(123),other=new IntPtr(456);
    Check(memory.DuringTransient(primary,0,0,1920,1080),"a capture or menu without any confirmed underlying state must keep the widget hidden");
    memory.Confirm(primary,0,0,1920,1080,false);
    Check(!memory.DuringTransient(primary,0,0,1920,1080),"capture over a confirmed normal desktop may retain the already safe indicator");
    Check(!memory.DuringTransient(primary,0,0,1920,1080),"a menu keeps the previous confirmed normal state without manufacturing a new state");
    memory.Confirm(primary,0,0,1920,1080,true);
    Check(memory.DuringTransient(primary,0,0,1920,1080),"capture over a confirmed borderless fullscreen app must not unhide the indicator");
    Check(memory.DuringTransient(primary,0,0,1920,1080),"a popup menu over fullscreen must retain fullscreen suppression");
    memory.Confirm(primary,0,0,1920,1080,false);
    Check(memory.DuringTransient(other,0,0,1920,1080),"another monitor cannot inherit the previous monitor's normal-desktop exception");
    Check(memory.DuringTransient(primary,0,0,2560,1440),"changed monitor geometry is unknown until an actual application is observed");
    Check(memory.DuringTransient(IntPtr.Zero,0,0,1920,1080),"no monitor is unknown even after a safe confirmation");
    Check(memory.DuringTransient(primary,0,0,0,1080),"invalid monitor width is unknown");
    Check(memory.DuringTransient(primary,0,0,1920,0),"invalid monitor height is unknown");
    memory.Confirm(primary,0,0,1920,1080,false);
    Check(!memory.DuringTransient(primary,0,0,1920,1080),"a newly confirmed normal foreground restores normal behavior");
    memory.Confirm(IntPtr.Zero,0,0,1920,1080,false);
    Check(memory.DuringTransient(primary,0,0,1920,1080),"an invalid confirmation invalidates the older safe state");
    memory.Confirm(primary,-1920,-1080,0,0,false);
    Check(!memory.DuringTransient(primary,-1920,-1080,0,0),"valid negative monitor coordinates remain a stable key");
  }
  [STAThread] static int Main() {
    try {
      FullscreenTransitions();
      Check(!TaskbarTransient.MaySuppressForFullscreen(IntPtr.Zero),"null foreground during a menu activation transition is not confirmed fullscreen");
      string windows=@"C:\Windows";
      foreach(string package in new[]{"MicrosoftWindows.Client.Core_cw5n1h2txyewy","MicrosoftWindows.Client.CBS_cw5n1h2txyewy","Microsoft.Windows.ShellExperienceHost_cw5n1h2txyewy"}) {
        string image=Path.Combine(windows,"SystemApps",package,"ScreenClippingHost.exe");
        Check(TaskbarTransient.IsTrustedCaptureImagePath(image,windows),"the exact protected Windows capture host path is recognized: "+package);
        Check(TaskbarTransient.IsTrustedCaptureImagePath(image.ToUpperInvariant(),windows.ToLowerInvariant()),"protected Windows capture paths are case insensitive");
      }
      Check(TaskbarTransient.IsTrustedCaptureImagePath(@"D:\Windows\SystemApps\MicrosoftWindows.Client.Core_cw5n1h2txyewy\ScreenClippingHost.exe",@"D:\Windows"),"Windows on another drive is supported");
      foreach(string image in new[]{
        @"C:\Tools\ScreenClippingHost.exe",
        @"C:\Users\user\Windows\SystemApps\MicrosoftWindows.Client.Core_cw5n1h2txyewy\ScreenClippingHost.exe",
        @"C:\Windows\SystemApps\Fake_cw5n1h2txyewy\ScreenClippingHost.exe",
        @"C:\Windows\SystemApps\MicrosoftWindows.Client.Core_cw5n1h2txyewy\SnippingTool.exe",
        @"C:\Windows\SystemApps\MicrosoftWindows.Client.Core_cw5n1h2txyewy\ScreenClippingHost.exe.bak",
        @"C:\Windows\SystemApps\MicrosoftWindows.Client.Core_cw5n1h2txyewy\ScreenClippingHost.exe:stream",
        @"C:\Windows\SystemApps\MicrosoftWindows.Client.Core_cw5n1h2txyewy\..\Fake\ScreenClippingHost.exe",
        @"C:\Windows.old\SystemApps\MicrosoftWindows.Client.Core_cw5n1h2txyewy\ScreenClippingHost.exe",
        @"Windows\SystemApps\MicrosoftWindows.Client.Core_cw5n1h2txyewy\ScreenClippingHost.exe",
        "",null,"\0"
      })Check(!TaskbarTransient.IsTrustedCaptureImagePath(image,windows),"lookalike, relative or invalid capture paths are rejected: "+image);
      foreach(uint flag in new uint[]{4,8,16,4|8|16,1|4})Check(TaskbarTransient.IsMenuMode(flag),"native menu flags are recognized");
      foreach(uint flag in new uint[]{0,1,2,1|2,32})Check(!TaskbarTransient.IsMenuMode(flag),"unrelated GUI flags are not menus");
      Check(TaskbarTransient.IsMenuClass("#32768"),"the standard native popup menu is transient rather than persistent taskbar occupancy");
      Check(TaskbarTransient.IsMenuClass("Xaml_WindowedPopupClass",true),"the current Explorer process's XAML popup is a transient shell menu");
      Check(!TaskbarTransient.IsMenuClass("Xaml_WindowedPopupClass",false),"another process's XAML popup cannot borrow the Explorer menu exception");
      Check(!TaskbarTransient.IsMenuClass("Xaml_WindowedPopupClass"),"XAML popup class alone is insufficient without current shell process identity");
      Check(TaskbarTransient.IsMenuClass("#32768",false),"the standard native popup menu does not require Explorer ownership");
      foreach(string name in new[]{"XamlExplorerHostIslandWindow","Windows.UI.Composition.DesktopWindowContentBridge","NetWorxDeskband","",null})Check(!TaskbarTransient.IsMenuClass(name,true),"Explorer ownership cannot exempt unrelated XAML hosts or widgets: "+name);
      foreach(string name in new[]{"#327680","NetWorxDeskband","MSTaskListWClass","Windows.UI.Composition.DesktopWindowContentBridge","",null})Check(!TaskbarTransient.IsMenuClass(name),"widgets and ordinary shell classes do not inherit a menu exemption");
      Check(!TaskbarTransient.IsCaptureWindow(IntPtr.Zero),"no HWND is not a capture window");
      IntPtr foreground=GetForegroundWindow();
      using(var own=new Form {ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Left=-20000,Top=-20000,Width=32,Height=32}) {
        IntPtr handle=own.Handle;
        for(int i=0;i<5;i++)Check(!TaskbarTransient.IsCaptureWindow(handle),"an ordinary own process cannot gain the Windows capture exception");
        Check(TaskbarTransient.MaySuppressForFullscreen(handle),"an ordinary application remains eligible for geometric fullscreen suppression");
        Check(GetForegroundWindow()==foreground,"identity checks and the unshown own fixture preserve foreground");
      }
      Console.WriteLine("PASS taskbar transient policy ("+assertions+" assertions)");return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}
  }
}
