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
      public bool Capture,Start11;
      public SafeFileHandle Process;
      public void Dispose(){if(Process!=null)Process.Dispose();}
    }
    sealed class ModuleIdentity:IDisposable {
      public long CheckedAt;public bool Trusted;public SafeFileHandle Process;
      public void Dispose(){if(Process!=null)Process.Dispose();}
    }
    const long IdentityLifetimeMilliseconds=1000;
    const int MaximumIdentities=128;
    const uint ProcessQueryLimitedInformation=0x1000,Synchronize=0x100000,WaitTimeout=258;
    static readonly object gate=new object();
    static readonly Stopwatch clock=Stopwatch.StartNew();
    static readonly Dictionary<uint,ImageIdentity> images=new Dictionary<uint,ImageIdentity>();
    static readonly Dictionary<Tuple<uint,IntPtr>,ModuleIdentity> modules=new Dictionary<Tuple<uint,IntPtr>,ModuleIdentity>();
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
    internal static bool IsTaskbarPopupDialog(string name,long style,long exStyle,bool nearBar,bool taskbarOwned,bool trustedStart11) {
      return String.Equals(name,"#32770",StringComparison.Ordinal)&&(style&0x80000000L)!=0&&(style&0x40000000L)==0&&(style&0x00C00000L)!=0x00C00000L&&(style&0x00040000L)==0&&(exStyle&0x00040000L)==0&&nearBar&&(taskbarOwned||trustedStart11);
    }
    static bool IsProtectedStart11File(string image,string programFilesX86,params string[] names) {
      try {
        if(String.IsNullOrWhiteSpace(image)||String.IsNullOrWhiteSpace(programFilesX86)||!Path.IsPathRooted(image)||!Path.IsPathRooted(programFilesX86))return false;
        string canonical=Path.GetFullPath(image),directory=Path.Combine(Path.GetFullPath(programFilesX86),"Stardock","Start11");
        foreach(string name in names)if(String.Equals(canonical,Path.Combine(directory,name),StringComparison.OrdinalIgnoreCase))return true;
      }catch(ArgumentException){}catch(NotSupportedException){}catch(PathTooLongException){}
      return false;
    }
    internal static bool IsTrustedStart11ImagePath(string image,string programFilesX86) {return IsProtectedStart11File(image,programFilesX86,"Start11_64.exe");}
    internal static bool IsTrustedStart11ModuleIdentity(string image,IntPtr instance,IntPtr moduleBase,string programFilesX86) {
      return instance!=IntPtr.Zero&&instance==moduleBase&&IsProtectedStart11File(image,programFilesX86,"Start10_64.dll","Start11_64.dll");
    }
    internal static bool IsMenuClass(string name) {return String.Equals(name,"#32768",StringComparison.Ordinal);}
    internal static bool IsMenuClass(string name,bool sameShellProcess) {return IsMenuClass(name)||(sameShellProcess&&String.Equals(name,"Xaml_WindowedPopupClass",StringComparison.Ordinal));}
    internal static bool IsMenuWindow(IntPtr window) {
      try {
      if(window==IntPtr.Zero)return false;
      var name=new StringBuilder(256);
      if(GetClassName(window,name,name.Capacity)<=0)return false;
      string value=name.ToString();bool nativeMenu=IsMenuClass(value);
      if(!nativeMenu) {
        bool xaml=String.Equals(value,"Xaml_WindowedPopupClass",StringComparison.Ordinal),dialog=String.Equals(value,"#32770",StringComparison.Ordinal);
        if(!xaml&&!dialog)return false;
        IntPtr shell=FindWindow("Shell_TrayWnd",null);uint shellProcess,windowProcess;
        if(shell==IntPtr.Zero||GetWindowThreadProcessId(shell,out shellProcess)==0||shellProcess==0||GetWindowThreadProcessId(window,out windowProcess)==0||windowProcess==0)return false;
        if(xaml) {if(!IsMenuClass(value,windowProcess==shellProcess))return false;}
        else {
          long style=GetWindowLongPtr(window,-16).ToInt64(),exStyle=GetWindowLongPtr(window,-20).ToInt64();Rect popupBounds,barBounds;
          if(!GetWindowRect(window,out popupBounds)||!GetWindowRect(shell,out barBounds)||!IsTaskbarPopupDialog(value,style,exStyle,NearTaskbar(popupBounds,barBounds),true,false))return false;
          bool shellOwned=windowProcess==shellProcess&&OwnedByShell(window,shell);
          bool start11=KnownProcess(windowProcess,true)||(windowProcess==shellProcess&&Start11Module(windowProcess,GetWindowLongPtr(window,-6)));
          if(!IsTaskbarPopupDialog(value,style,exStyle,true,shellOwned,start11))return false;
        }
      }
      lock(gate){lastMenuWindow=window;}
      return true;
      }catch{return false;}
    }
    static bool NearTaskbar(Rect popup,Rect bar) {
      long width=(long)bar.Right-bar.Left,height=(long)bar.Bottom-bar.Top;
      if(width<=0||height<=0||popup.Right<=popup.Left||popup.Bottom<=popup.Top)return false;
      long allowance=Math.Max(12,Math.Min(width,height)/2);
      if(width>height)return popup.Left<bar.Right&&popup.Right>bar.Left&&Math.Max(0,Math.Max((long)bar.Top-popup.Bottom,(long)popup.Top-bar.Bottom))<=allowance;
      return popup.Top<bar.Bottom&&popup.Bottom>bar.Top&&Math.Max(0,Math.Max((long)bar.Left-popup.Right,(long)popup.Left-bar.Right))<=allowance;
    }
    static bool OwnedByShell(IntPtr window,IntPtr shell) {
      IntPtr owner=GetWindow(window,4);
      for(int i=0;i<16&&owner!=IntPtr.Zero;i++){if(owner==shell||GetAncestor(owner,2)==shell)return true;owner=GetWindow(owner,4);}
      return false;
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
      bool foregroundMenu=false;
      try {
        IntPtr foreground=GetForegroundWindow();
        foregroundMenu=IsWindowVisible(foreground)&&!Cloaked(foreground)&&IsMenuWindow(foreground);
        if(IsWindowVisible(foreground)&&!Cloaked(foreground)&&IsCaptureWindow(foreground))captureWindow=foreground;
        else {
          IntPtr remembered;uint expected;
          lock(gate){remembered=lastCaptureWindow;expected=lastCaptureProcess;}
          uint current;
          if(remembered!=IntPtr.Zero&&IsWindowVisible(remembered)&&!Cloaked(remembered)&&GetWindowThreadProcessId(remembered,out current)!=0&&current==expected&&IsCaptureWindow(remembered))captureWindow=remembered;
          else if(remembered!=IntPtr.Zero)lock(gate){if(lastCaptureWindow==remembered){lastCaptureWindow=IntPtr.Zero;lastCaptureProcess=0;}}
        }
      }catch{}
      return captureWindow!=IntPtr.Zero||foregroundMenu||MenuActive()||VisibleRememberedMenu()||FindMenuAboveForegroundShell();
    }
    static bool FindMenuAboveForegroundShell() {
      IntPtr shell=FindWindow("Shell_TrayWnd",null),foreground=GetForegroundWindow();
      if(shell==IntPtr.Zero)return false;
      if(foreground!=IntPtr.Zero&&foreground!=shell) {
        var kind=new StringBuilder(256);GetClassName(foreground,kind,kind.Capacity);
        // Start11 may focus a small auxiliary dialog while its proven menu is
        // displayed by a separate window. The search still accepts only menus
        // whose own shell/module provenance is verified.
        if(kind.ToString()!="#32770")return false;
      }
      bool found=false;int visited=0;
      EnumWindows(delegate(IntPtr window,IntPtr unused){if(window==shell||++visited>128)return false;if(IsWindowVisible(window)&&!Cloaked(window)&&IsMenuWindow(window)){found=true;return false;}return true;},IntPtr.Zero);
      return found;
    }
    static bool VisibleRememberedMenu() {
      IntPtr remembered;lock(gate){remembered=lastMenuWindow;}
      if(remembered==IntPtr.Zero)return false;
      if(IsWindowVisible(remembered)&&!Cloaked(remembered)&&IsMenuWindow(remembered))return true;
      lock(gate){if(lastMenuWindow==remembered)lastMenuWindow=IntPtr.Zero;}
      return false;
    }
    static bool Cloaked(IntPtr window){int cloaked;return DwmGetWindowAttribute(window,14,out cloaked,4)==0&&cloaked!=0;}
    static bool CaptureProcess(uint process) {return KnownProcess(process,false);}
    static bool KnownProcess(uint process,bool start11) {
      lock(gate) {
        long now=clock.ElapsedMilliseconds;ImageIdentity identity;
        if(images.TryGetValue(process,out identity)) {
          // Holding the process handle and checking liveness prevents a reused PID inheriting an exception.
          if(now-identity.CheckedAt<IdentityLifetimeMilliseconds&&(identity.Process==null||WaitForSingleObject(identity.Process,0)==WaitTimeout))return start11?identity.Start11:identity.Capture;
          images.Remove(process);identity.Dispose();
        }
        Prune(now);
        identity=new ImageIdentity{CheckedAt=now};
        IntPtr raw=OpenProcess(ProcessQueryLimitedInformation|Synchronize,false,process);
        if(raw!=IntPtr.Zero) {
          identity.Process=new SafeFileHandle(raw,true);
          var image=new StringBuilder(32768);uint length=(uint)image.Capacity;
          if(QueryFullProcessImageName(identity.Process,0,image,ref length)&&WaitForSingleObject(identity.Process,0)==WaitTimeout) {
            identity.Capture=IsTrustedCaptureImagePath(image.ToString(),Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            identity.Start11=IsTrustedStart11ImagePath(image.ToString(),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
          }
        }
        images.Add(process,identity);return start11?identity.Start11:identity.Capture;
      }
    }
    static bool Start11Module(uint process,IntPtr instance) {
      if(instance==IntPtr.Zero)return false;
      lock(gate) {
        long now=clock.ElapsedMilliseconds;var key=Tuple.Create(process,instance);ModuleIdentity identity;
        if(modules.TryGetValue(key,out identity)) {
          if(identity.Process!=null&&WaitForSingleObject(identity.Process,0)==WaitTimeout&&(identity.Trusted||now-identity.CheckedAt<IdentityLifetimeMilliseconds))return identity.Trusted;
          modules.Remove(key);identity.Dispose();
        }
        if(modules.Count>=64) {
          Tuple<uint,IntPtr> oldest=null;long time=Int64.MaxValue;
          foreach(var item in modules)if(item.Value.CheckedAt<time){oldest=item.Key;time=item.Value.CheckedAt;}
          if(oldest!=null){modules[oldest].Dispose();modules.Remove(oldest);}
        }
        identity=new ModuleIdentity{CheckedAt=now};IntPtr raw=OpenProcess(ProcessQueryLimitedInformation|Synchronize,false,process);
        if(raw!=IntPtr.Zero) {
          identity.Process=new SafeFileHandle(raw,true);
          try {
            using(var owner=Process.GetProcessById((int)process))foreach(ProcessModule module in owner.Modules)if(module.BaseAddress==instance) {
              identity.Trusted=IsTrustedStart11ModuleIdentity(module.FileName,instance,module.BaseAddress,Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));break;
            }
          }catch{}
          if(WaitForSingleObject(identity.Process,0)!=WaitTimeout)identity.Trusted=false;
        }
        modules.Add(key,identity);return identity.Trusted;
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
    delegate bool EnumWindowProc(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowProc callback,IntPtr parameter);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect rectangle);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flags);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int capacity);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string className,string title);
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint desiredAccess,bool inheritHandle,uint process);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(SafeFileHandle process,uint flags,StringBuilder image,ref uint length);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(SafeFileHandle process,uint milliseconds);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,uint attribute,out int value,int size);
  }
}
