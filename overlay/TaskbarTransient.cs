using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace QuotaOverlay {
  internal sealed class TaskbarFullscreenMemory {
    readonly object gate=new object();
    bool confirmed,fullscreen;
    IntPtr monitor;
    int left,top,right,bottom;
    static bool Valid(IntPtr monitor,int left,int top,int right,int bottom) {return monitor!=IntPtr.Zero&&right>left&&bottom>top;}
    internal void Confirm(IntPtr monitor,int left,int top,int right,int bottom,bool fullscreen) {
      lock(gate) {
        if(!Valid(monitor,left,top,right,bottom)){confirmed=false;return;}
        this.monitor=monitor;this.left=left;this.top=top;this.right=right;this.bottom=bottom;
        this.fullscreen=fullscreen;confirmed=true;
      }
    }
    internal bool DuringTransient(IntPtr monitor,int left,int top,int right,int bottom) {
      lock(gate) {
        if(!confirmed||!Valid(monitor,left,top,right,bottom)||this.monitor!=monitor||this.left!=left||this.top!=top||this.right!=right||this.bottom!=bottom)return true;
        return fullscreen;
      }
    }
  }
  // Read-only identity and menu checks. No hooks, input injection or process enumeration.
  internal static class TaskbarTransient {
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct GuiThreadInfo {
      public uint Size,Flags;
      public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;
      public Rect CaretBounds;
    }
    sealed class ImageIdentity:IDisposable {
      public long CheckedAt;
      public bool Capture;
      public SafeFileHandle Process;
      public void Dispose(){if(Process!=null)Process.Dispose();}
    }
    const long IdentityLifetimeMilliseconds=1000;
    const int MaximumIdentities=128;
    const uint ProcessQueryLimitedInformation=0x1000,Synchronize=0x100000,WaitTimeout=258;
    static readonly object gate=new object();
    static readonly Stopwatch clock=Stopwatch.StartNew();
    static readonly Dictionary<uint,ImageIdentity> images=new Dictionary<uint,ImageIdentity>();
    static IntPtr lastCaptureWindow,lastMenuWindow;
    static uint lastCaptureProcess;

    // Missing foreground during activation is not evidence of an exclusive fullscreen app.
    internal static bool MaySuppressForFullscreen(IntPtr foreground) {return foreground!=IntPtr.Zero&&!IsCaptureWindow(foreground)&&!IsMenuWindow(foreground);}
    internal static bool IsTrustedCaptureImagePath(string image,string windowsDirectory) {
      try {
        if(String.IsNullOrWhiteSpace(image)||String.IsNullOrWhiteSpace(windowsDirectory)||!Path.IsPathRooted(image)||!Path.IsPathRooted(windowsDirectory))return false;
        string canonical=Path.GetFullPath(image),systemApps=Path.Combine(Path.GetFullPath(windowsDirectory),"SystemApps");
        foreach(string package in new[]{"MicrosoftWindows.Client.Core_cw5n1h2txyewy","MicrosoftWindows.Client.CBS_cw5n1h2txyewy","Microsoft.Windows.ShellExperienceHost_cw5n1h2txyewy"})
          if(String.Equals(canonical,Path.Combine(systemApps,package,"ScreenClippingHost.exe"),StringComparison.OrdinalIgnoreCase))return true;
      }catch(ArgumentException){}catch(NotSupportedException){}catch(PathTooLongException){}
      return false;
    }
    internal static bool IsMenuMode(uint flags) {return (flags&0x1C)!=0;}
    internal static bool IsMenuClass(string name) {return String.Equals(name,"#32768",StringComparison.Ordinal);}
    internal static bool IsMenuClass(string name,bool sameShellProcess) {return IsMenuClass(name)||(sameShellProcess&&String.Equals(name,"Xaml_WindowedPopupClass",StringComparison.Ordinal));}
    internal static bool IsMenuWindow(IntPtr window) {
      if(window==IntPtr.Zero)return false;
      var name=new StringBuilder(256);
      if(GetClassName(window,name,name.Capacity)<=0)return false;
      string value=name.ToString();bool nativeMenu=IsMenuClass(value);
      if(!nativeMenu) {
        if(!String.Equals(value,"Xaml_WindowedPopupClass",StringComparison.Ordinal))return false;
        IntPtr shell=FindWindow("Shell_TrayWnd",null);uint shellProcess,windowProcess;
        if(shell==IntPtr.Zero||GetWindowThreadProcessId(shell,out shellProcess)==0||shellProcess==0||GetWindowThreadProcessId(window,out windowProcess)==0||windowProcess==0||!IsMenuClass(value,windowProcess==shellProcess))return false;
      }
      lock(gate){lastMenuWindow=window;}
      return true;
    }
    internal static bool MenuActive() {
      try {
        var information=new GuiThreadInfo{Size=(uint)Marshal.SizeOf(typeof(GuiThreadInfo))};
        if(GetGUIThreadInfo(0,ref information)&&IsMenuMode(information.Flags))return true;
        IntPtr foreground=GetForegroundWindow();uint process;
        uint thread=foreground==IntPtr.Zero?0:GetWindowThreadProcessId(foreground,out process);
        information=new GuiThreadInfo{Size=(uint)Marshal.SizeOf(typeof(GuiThreadInfo))};
        return thread!=0&&GetGUIThreadInfo(thread,ref information)&&IsMenuMode(information.Flags);
      }catch{return false;}
    }
    internal static bool IsCaptureWindow(IntPtr window) {
      try {
        if(window==IntPtr.Zero)return false;
        uint process;GetWindowThreadProcessId(window,out process);
        if(process==0||!CaptureProcess(process))return false;
        lock(gate){lastCaptureWindow=window;lastCaptureProcess=process;}
        return true;
      }catch{return false;}
    }
    internal static bool IsActive(out IntPtr captureWindow) {
      captureWindow=IntPtr.Zero;
      try {
        IntPtr foreground=GetForegroundWindow();
        if(IsWindowVisible(foreground)&&!Cloaked(foreground)&&IsCaptureWindow(foreground))captureWindow=foreground;
        else {
          IntPtr remembered;uint expected;
          lock(gate){remembered=lastCaptureWindow;expected=lastCaptureProcess;}
          uint current;
          if(remembered!=IntPtr.Zero&&IsWindowVisible(remembered)&&!Cloaked(remembered)&&GetWindowThreadProcessId(remembered,out current)!=0&&current==expected&&IsCaptureWindow(remembered))captureWindow=remembered;
          else if(remembered!=IntPtr.Zero)lock(gate){if(lastCaptureWindow==remembered){lastCaptureWindow=IntPtr.Zero;lastCaptureProcess=0;}}
        }
      }catch{}
      return captureWindow!=IntPtr.Zero||MenuActive()||VisibleRememberedMenu();
    }
    static bool VisibleRememberedMenu() {
      IntPtr remembered;lock(gate){remembered=lastMenuWindow;}
      if(remembered==IntPtr.Zero)return false;
      if(IsWindowVisible(remembered)&&!Cloaked(remembered)&&IsMenuWindow(remembered))return true;
      lock(gate){if(lastMenuWindow==remembered)lastMenuWindow=IntPtr.Zero;}
      return false;
    }
    static bool Cloaked(IntPtr window){int cloaked;return DwmGetWindowAttribute(window,14,out cloaked,4)==0&&cloaked!=0;}
    static bool CaptureProcess(uint process) {
      lock(gate) {
        long now=clock.ElapsedMilliseconds;ImageIdentity identity;
        if(images.TryGetValue(process,out identity)) {
          // Holding the process handle and checking liveness prevents a reused PID inheriting an exception.
          if(now-identity.CheckedAt<IdentityLifetimeMilliseconds&&(identity.Process==null||WaitForSingleObject(identity.Process,0)==WaitTimeout))return identity.Capture;
          images.Remove(process);identity.Dispose();
        }
        Prune(now);
        identity=new ImageIdentity{CheckedAt=now};
        IntPtr raw=OpenProcess(ProcessQueryLimitedInformation|Synchronize,false,process);
        if(raw!=IntPtr.Zero) {
          identity.Process=new SafeFileHandle(raw,true);
          var image=new StringBuilder(32768);uint length=(uint)image.Capacity;
          identity.Capture=QueryFullProcessImageName(identity.Process,0,image,ref length)&&
            IsTrustedCaptureImagePath(image.ToString(),Environment.GetFolderPath(Environment.SpecialFolder.Windows))&&
            WaitForSingleObject(identity.Process,0)==WaitTimeout;
        }
        images.Add(process,identity);return identity.Capture;
      }
    }
    static void Prune(long now) {
      if(images.Count<MaximumIdentities)return;
      var expired=new List<uint>();uint oldest=0;long earliest=Int64.MaxValue;
      foreach(var item in images){if(now-item.Value.CheckedAt>=IdentityLifetimeMilliseconds)expired.Add(item.Key);if(item.Value.CheckedAt<earliest){earliest=item.Value.CheckedAt;oldest=item.Key;}}
      if(expired.Count==0)expired.Add(oldest);
      foreach(uint process in expired){images[process].Dispose();images.Remove(process);}
    }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread,ref GuiThreadInfo information);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int capacity);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string className,string title);
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint desiredAccess,bool inheritHandle,uint process);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(SafeFileHandle process,uint flags,StringBuilder image,ref uint length);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(SafeFileHandle process,uint milliseconds);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,uint attribute,out int value,int size);
  }
}
