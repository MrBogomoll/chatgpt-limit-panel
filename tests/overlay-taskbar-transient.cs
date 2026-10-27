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
  static void TaskbarDialogPolicy(){
    const long popup=0x84000000L,actualEx=0x082800A8L;
    Check(TaskbarTransient.IsTaskbarPopupDialog("#32770",popup,actualEx,true,false,true),"the observed captionless Start11 context dialog is recognized by trusted provenance");
    Check(TaskbarTransient.IsTaskbarPopupDialog("#32770",popup,actualEx,true,true,false),"a positively shell-owned captionless popup is recognized");
    Check(!TaskbarTransient.IsTaskbarPopupDialog("#32770",popup,actualEx,true,false,false),"a foreign captionless dialog cannot inherit the taskbar menu exception");
    Check(!TaskbarTransient.IsTaskbarPopupDialog("#32770",popup,actualEx,false,true,true),"unrelated dialogs away from the taskbar remain protected");
    foreach(long style in new[]{0L,popup|0x40000000L,popup|0x00C00000L,popup|0x00040000L})Check(!TaskbarTransient.IsTaskbarPopupDialog("#32770",style,actualEx,true,true,true),"ordinary framed, child, resizable or nonpopup dialogs are excluded");
    Check(!TaskbarTransient.IsTaskbarPopupDialog("#32770",popup,actualEx|0x00040000L,true,true,true),"an app-window dialog remains a real application");
    foreach(string name in new[]{"#32768","#327700","NetWorxDeskband","Chrome_WidgetWin_1","",null})Check(!TaskbarTransient.IsTaskbarPopupDialog(name,popup,actualEx,true,true,true),"dialog provenance never exempts unrelated window classes");
    string root=@"C:\Program Files (x86)";IntPtr instance=new IntPtr(0x12345000);
    Check(TaskbarTransient.IsTrustedStart11ImagePath(Path.Combine(root,"Stardock","Start11","Start11_64.exe"),root),"the exact protected standalone Start11 image is recognized");
    foreach(string module in new[]{"Start10_64.dll","Start11_64.dll"}){
      string image=Path.Combine(root,"Stardock","Start11",module);
      Check(TaskbarTransient.IsTrustedStart11ModuleIdentity(image,instance,instance,root),"the dialog instance must resolve to an exact protected Start11 module");
      Check(TaskbarTransient.IsTrustedStart11ModuleIdentity(image.ToUpperInvariant(),instance,instance,root.ToLowerInvariant()),"module identity paths are case insensitive");
      Check(!TaskbarTransient.IsTrustedStart11ModuleIdentity(image,instance,new IntPtr(0x23456000),root),"a trusted module path with another base address cannot prove the dialog instance");
      Check(!TaskbarTransient.IsTrustedStart11ModuleIdentity(image,IntPtr.Zero,IntPtr.Zero,root),"a missing instance never proves Start11 provenance");
    }
    foreach(string image in new[]{@"C:\Temp\Start10_64.dll",@"C:\Program Files (x86)\Stardock\Start11Fake\Start10_64.dll",@"C:\Program Files (x86)\Stardock\Start10\Start10_64.dll",@"C:\Program Files (x86)\Stardock\Start11\Start10_64.dll:stream",@"C:\Program Files (x86)\Stardock\Start11\Start10_64.dll.bak",@"Stardock\Start11\Start10_64.dll","",null,"\0"})Check(!TaskbarTransient.IsTrustedStart11ModuleIdentity(image,instance,instance,root),"fake, relative and invalid Start11 module paths are rejected");
    foreach(string image in new[]{@"C:\Temp\Start11_64.exe",@"C:\Program Files (x86)\Stardock\Start11\Start11.exe",@"C:\Program Files (x86)\Stardock\Start11Fake\Start11_64.exe",@"C:\Program Files (x86)\Stardock\Start11\Start11_64.exe:stream","",null})Check(!TaskbarTransient.IsTrustedStart11ImagePath(image,root),"a filename alone cannot establish standalone Start11 provenance");
  }
  static bool Modal(long style,long extended,bool[] guards){return TaskbarTransient.IsTaskbarModalDialog("#32770",style,extended,guards[0],guards[1],guards[2],guards[3],guards[4],guards[5],guards[6],guards[7],guards[8]);}
  static void NullInstanceModalPolicy(){
    const long style=0x96000001L,extended=0x00080088L;var valid=new[]{true,true,true,true,true,true,true,true,true};
    Check(Modal(style,extended,valid),"the exact observed foreground taskbar-thread Start11 modal signature is eligible only for position retention");
    Check(Modal(unchecked((int)0x96000001),extended,valid),"native signed and unsigned representations of the observed window style are equivalent");
    for(int i=0;i<valid.Length;i++){var changed=(bool[])valid.Clone();changed[i]=false;Check(!Modal(style,extended,changed),"each live modal provenance or geometry guard is independently required: "+i);}
    foreach(long bit in new long[]{0x80000000L,0x10000000L,0x04000000L,0x02000000L,1})Check(!Modal(style&~bit,extended,valid),"each observed popup style bit is required");
    foreach(long bit in new long[]{0x40000000L,0x08000000L,0x00C00000L,0x00040000L,0x00080000L})Check(!Modal(style|bit,extended,valid),"child, disabled, captioned, resizable and system-menu dialogs are not the taskbar modal");
    foreach(long bit in new long[]{0x00080000L,0x80L,8L})Check(!Modal(style,extended&~bit,valid),"layered, tool-window and topmost extended styles are independently required");
    foreach(long bit in new long[]{0x00040000L,0x08000000L})Check(!Modal(style,extended|bit,valid),"app-window and no-activate shapes do not borrow the active-modal signature");
    Check(!TaskbarTransient.IsTaskbarModalDialog("NetWorxDeskband",style,extended,true,true,true,true,true,true,true,true,true),"a class name mismatch cannot borrow shell thread and module evidence");
    string root=@"C:\Program Files (x86)";
    foreach(string name in new[]{"Start10_64.dll","Start11_64.dll"})Check(TaskbarTransient.IsLoadedStart11ModulePath(Path.Combine(root,"Stardock","Start11",name),root),"the independent loaded-module guard accepts the exact protected Start11 DLL");
    foreach(string image in new[]{@"C:\Temp\Start10_64.dll",@"C:\Program Files (x86)\Stardock\Start11Fake\Start10_64.dll",@"C:\Program Files (x86)\Stardock\Start11\Start10_64.dll:stream",null,""})Check(!TaskbarTransient.IsLoadedStart11ModulePath(image,root),"a fake or missing module cannot establish loaded Start11 presence");
  }
  [STAThread] static int Main() {
    try {
      FullscreenTransitions();
      TaskbarDialogPolicy();
      NullInstanceModalPolicy();
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
