using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace QuotaOverlay {
  // WPF Window uses the older top-level-only transparency path. A layered
  // child must be created as a child, with modern per-pixel transparency.
  internal sealed class TaskbarChildSurface:IDisposable {
    readonly FrameworkElement root;
    HwndSource source;
    Action closed;
    bool disposed,reported;
    internal TaskbarChildSurface(FrameworkElement root){if(root==null)throw new ArgumentNullException("root");this.root=root;}
    internal IntPtr Handle {get{return source==null||disposed?IntPtr.Zero:source.Handle;}}
    internal bool IsAlive {get{return Handle!=IntPtr.Zero&&IsWindow(Handle);}}
    internal bool IsVisible {get{return IsAlive&&IsWindowVisible(Handle);}}
    internal double Opacity {get{return root.Opacity;}set{root.Opacity=value;}}
    internal bool TryCreate(IntPtr parent,HwndSourceHook hook,Action onClosed,out string reason) {
      reason="child-creation-unavailable";
      if(disposed||source!=null||!root.Dispatcher.CheckAccess())return false;
      try {
        IntPtr thread=GetThreadDpiAwarenessContext(),host=GetWindowDpiAwarenessContext(parent);
        if(parent==IntPtr.Zero||!IsWindow(parent)||thread==IntPtr.Zero||host==IntPtr.Zero||!AreDpiAwarenessContextsEqual(thread,host)){reason="child-creation-dpi-mismatch";return false;}
        closed=onClosed;
        var parameters=new HwndSourceParameters("ChatGPT Limit Panel taskbar"){
          ParentWindow=parent,WindowStyle=0x46000000,ExtendedWindowStyle=0x08000080,
          Width=1,Height=1,UsesPerPixelTransparency=true,
          AcquireHwndFocusInMenuMode=false,TreatAsInputRoot=true
        };
        source=new HwndSource(parameters);
        source.AddHook(delegate(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled){
          if(message==0x21){handled=true;return new IntPtr(3);}
          return hook==null?IntPtr.Zero:hook(h,message,w,l,ref handled);
        });
        source.Disposed+=delegate{ReportClosed();};
        source.CompositionTarget.BackgroundColor=Colors.Transparent;
        source.RootVisual=root;
        if(!IsAlive||GetParent(Handle)!=parent||!AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(Handle),host)){reason="child-creation-unverified";Dispose();return false;}
        reason="child-created";return true;
      }catch{reason="child-creation-error";Dispose();return false;}
    }
    internal void Show(){if(IsAlive)ShowWindow(Handle,4);}
    internal void Hide(){if(IsAlive)ShowWindow(Handle,0);}
    void ReportClosed(){if(reported)return;reported=true;if(closed!=null)closed();}
    public void Dispose(){if(disposed)return;disposed=true;var previous=source;source=null;if(previous!=null)previous.Dispose();ReportClosed();}
    [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")]static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")]static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")]static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
  }
}
