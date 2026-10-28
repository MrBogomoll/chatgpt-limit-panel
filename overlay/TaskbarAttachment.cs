using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace QuotaOverlay {
  // Reparents only a caller-owned HWND. It never changes a host window, shows
  // pixels, calls a DPI-awareness setter, reserves space or restarts the shell.
  internal sealed class TaskbarAttachment:IDisposable {
    const long Child=0x40000000L,Popup=0x80000000L,Topmost=8,NoActivate=0x08000000L,ToolWindow=0x80,AppWindow=0x40000;
    const uint NoSize=1,NoMove=2,NoZOrder=4,NoActivateFlag=0x10,FrameChanged=0x20,NoOwnerZOrder=0x200,WaitTimeout=258;
    readonly Func<IntPtr> resolveHost;
    readonly Func<IntPtr,bool> validateHost;
    readonly Func<IntPtr,IntPtr,IntPtr> setParent;
    IntPtr own,parent;
    uint parentPid,parentThread;
    SafeFileHandle parentProcess;
    bool disposed;
    [StructLayout(LayoutKind.Sequential)]struct Rect {internal int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]struct Point {internal int X,Y;}
    sealed class Host {internal IntPtr Window,Context;internal uint Pid,Thread,Dpi;internal Rect Bounds,Client;}

    internal TaskbarAttachment(Func<IntPtr> resolveHost=null,Func<IntPtr,bool> validateHost=null,Func<IntPtr,IntPtr,IntPtr> setParent=null) {
      this.resolveHost=resolveHost??delegate{return FindWindow("Shell_TrayWnd",null);};
      this.validateHost=validateHost??IsShellHost;
      this.setParent=setParent??NativeSetParent;
    }
    internal IntPtr Parent {get{return parent;}}
    // Hidden/iconic parents remain the same generation. Visibility is a
    // placement condition, not a reason to recreate a still-live WPF child.
    internal bool IsAlive {get{try{Host host;string reason;return !disposed&&own!=IntPtr.Zero&&Owned(own)&&ReadHost(out host,out reason)&&SameGeneration(host)&&Alive(parentProcess);}catch{return false;}}}
    internal bool IsAttached {get{try{long style,extended;return IsAlive&&GetParent(own)==parent&&ReadLong(own,-16,out style)&&ReadLong(own,-20,out extended)&&(style&Child)!=0&&(style&Popup)==0&&(extended&Topmost)==0&&SameDpi(own,parent);}catch{return false;}}}

    internal bool TryAttach(IntPtr candidate,out string reason) {
      reason="attachment-unavailable";SafeFileHandle process=null;long originalStyle=0,originalExtended=0;bool changedStyle=false;IntPtr requestedParent=IntPtr.Zero;
      try {
        if(disposed){reason="attachment-disposed";return false;}
        if(!Owned(candidate)){reason="own-window-unavailable";return false;}
        if(own!=IntPtr.Zero&&own!=candidate){reason="own-window-changed";return false;}
        Host host;if(!ReadHost(out host,out reason))return false;requestedParent=host.Window;
        if(parent!=IntPtr.Zero&&(!SameGeneration(host)||!Alive(parentProcess))){reason="host-generation-changed";return false;}
        if(IsIconic(host.Window)){reason="host-iconic";return false;}
        if(!SameDpi(candidate,host.Window)){reason="dpi-context-mismatch";return false;}
        if(!ReadLong(candidate,-16,out originalStyle)||!ReadLong(candidate,-20,out originalExtended)){reason="own-style-unavailable";return false;}
        if((originalStyle&Child)!=0&&GetParent(candidate)!=host.Window){reason="unexpected-parent";return false;}
        if(parent==IntPtr.Zero){process=OpenProcess(0x101000,false,host.Pid);if(!Alive(process)){reason="host-process-unavailable";return false;}}
        Host current;if(!ReadHost(out current,out reason)||!EqualIdentity(host,current)||!SameDpi(candidate,current.Window)){reason="host-changed-before-attach";return false;}
        // A previously attached healthy child needs no frame/style operations.
        bool already=(originalStyle&Child)!=0&&GetParent(candidate)==host.Window;
        long desiredExtended=(originalExtended|NoActivate|ToolWindow)&~(Topmost|AppWindow);
        if(already&&originalExtended==desiredExtended){Remember(candidate,host,ref process);if(!IsAttached){reason="attachment-unverified";return false;}reason="attached";return true;}
        if((originalExtended&Topmost)!=0&&!SetWindowPos(candidate,new IntPtr(-2),0,0,0,0,NoSize|NoMove|NoActivateFlag|NoOwnerZOrder)){reason="own-band-failed";return false;}
        changedStyle=true;
        if(!WriteLong(candidate,-16,(originalStyle|Child)&~Popup)||!WriteLong(candidate,-20,desiredExtended)){reason="own-style-failed";return false;}
        // Compare again immediately before SetParent. Different DPI modes must
        // never cause Windows to reset this GUI process's awareness.
        if(!ReadHost(out current,out reason)||!EqualIdentity(host,current)||!SameDpi(candidate,current.Window)){reason="host-changed-before-parent";return false;}
        if(!already){NativeSetLastError(0);IntPtr previous=setParent(candidate,host.Window);int error=Marshal.GetLastWin32Error();if((previous==IntPtr.Zero&&error!=0)||GetParent(candidate)!=host.Window){reason="set-parent-failed";return false;}}
        Remember(candidate,host,ref process);
        if(!SetWindowPos(candidate,IntPtr.Zero,0,0,0,0,NoSize|NoMove|NoZOrder|NoActivateFlag|NoOwnerZOrder|FrameChanged)){reason="child-frame-failed";return false;}
        if(!IsAttached){reason="attachment-unverified";return false;}
        changedStyle=false;reason="attached";return true;
      }catch{reason="attachment-native-error";return false;}
      finally {
        if(process!=null)process.Dispose();
        // A failed SetParent leaves this own HWND unhosted. Restore its original
        // top-level shape without restoring an independent TOPMOST fallback.
        if(changedStyle&&IsWindow(candidate)&&GetParent(candidate)!=requestedParent) {
          WriteLong(candidate,-16,originalStyle);WriteLong(candidate,-20,originalExtended&~Topmost);
          SetWindowPos(candidate,IntPtr.Zero,0,0,0,0,NoSize|NoMove|NoZOrder|NoActivateFlag|NoOwnerZOrder|FrameChanged);
        }
      }
    }

    internal bool TryPlace(IntPtr candidate,TaskbarSnapshot snapshot,TaskbarArea bounds,out string reason) {
      reason="placement-unavailable";
      try {
        if(disposed){reason="attachment-disposed";return false;}
        Host host;if(!ReadHost(out host,out reason))return false;
        if(!ValidatePlacement(host,snapshot,bounds,out reason))return false;
        if(!TryAttach(candidate,out reason))return false;
        // SetParent may deliver WPF/native messages. Do not trust earlier
        // coordinates if the host moved, changed DPI or was recreated meanwhile.
        if(!ReadHost(out host,out reason)||!SameGeneration(host)||!ValidatePlacement(host,snapshot,bounds,out reason))return false;
        var first=new Point{X=(int)bounds.X,Y=(int)bounds.Y};var last=new Point{X=(int)(bounds.X+bounds.Width),Y=(int)(bounds.Y+bounds.Height)};
        if(!Map(host.Window,ref first)||!Map(host.Window,ref last)){reason="client-mapping-failed";return false;}
        if(first.X<host.Client.Left||first.Y<host.Client.Top||last.X>host.Client.Right||last.Y>host.Client.Bottom||last.X-first.X!=(int)bounds.Width||last.Y-first.Y!=(int)bounds.Height){reason="outside-host-client";return false;}
        if(!SetWindowPos(candidate,IntPtr.Zero,first.X,first.Y,(int)bounds.Width,(int)bounds.Height,NoActivateFlag|NoOwnerZOrder)){reason="child-position-failed";return false;}
        Host after;Rect actual;if(!ReadHost(out after,out reason)||!SameGeneration(after)||!ValidatePlacement(after,snapshot,bounds,out reason)||!IsAttached||!GetWindowRect(candidate,out actual)||!Equal(actual,bounds)||GetDpiForWindow(candidate)!=after.Dpi){reason="child-position-unverified";return false;}
        reason="attached";return true;
      }catch{reason="placement-native-error";return false;}
    }

    bool ReadHost(out Host host,out string reason) {
      host=null;reason="host-unavailable";IntPtr value=resolveHost();
      if(value==IntPtr.Zero||!IsWindow(value)||!validateHost(value))return false;
      uint pid;uint thread=GetWindowThreadProcessId(value,out pid);Rect bounds,client;long style;
      if(pid==0||thread==0||!ReadLong(value,-16,out style)||(style&Child)!=0||GetParent(value)!=IntPtr.Zero||GetAncestor(value,2)!=value||!GetWindowRect(value,out bounds)||!GetClientRect(value,out client)||bounds.Right<=bounds.Left||bounds.Bottom<=bounds.Top||client.Right<=client.Left||client.Bottom<=client.Top)return false;
      IntPtr context=GetWindowDpiAwarenessContext(value);uint dpi=GetDpiForWindow(value);if(context==IntPtr.Zero||dpi==0||dpi>768){reason="host-dpi-unavailable";return false;}
      host=new Host{Window=value,Pid=pid,Thread=thread,Bounds=bounds,Client=client,Context=context,Dpi=dpi};return true;
    }
    static bool ValidatePlacement(Host host,TaskbarSnapshot snapshot,TaskbarArea bounds,out string reason) {
      reason="invalid-placement";
      if(snapshot==null||snapshot.Handle!=host.Window||!snapshot.Visible||snapshot.AutoHidden||!TaskbarLayout.Valid(snapshot.Bounds)||!TaskbarLayout.Valid(bounds)||!TaskbarLayout.Finite(snapshot.Scale)||snapshot.Scale<=0||snapshot.Scale>8||Math.Abs(snapshot.Scale-host.Dpi/96.0)>0.0001||!Equal(host.Bounds,snapshot.Bounds))return false;
      if(!IsWindowVisible(host.Window)){reason="host-hidden";return false;}if(IsIconic(host.Window)){reason="host-iconic";return false;}
      if(!Integer(bounds.X)||!Integer(bounds.Y)||!Integer(bounds.Width)||!Integer(bounds.Height)||!Integer(bounds.X+bounds.Width)||!Integer(bounds.Y+bounds.Height)||bounds.X<snapshot.Bounds.X||bounds.Y<snapshot.Bounds.Y||bounds.X+bounds.Width>snapshot.Bounds.X+snapshot.Bounds.Width||bounds.Y+bounds.Height>snapshot.Bounds.Y+snapshot.Bounds.Height)return false;
      return true;
    }
    static bool Integer(double value){return TaskbarLayout.Finite(value)&&value>=Int32.MinValue&&value<=Int32.MaxValue&&Math.Truncate(value)==value;}
    static bool Equal(Rect rect,TaskbarArea area){return rect.Left==area.X&&rect.Top==area.Y&&rect.Right-rect.Left==area.Width&&rect.Bottom-rect.Top==area.Height;}
    static bool EqualIdentity(Host first,Host second){return first.Window==second.Window&&first.Pid==second.Pid&&first.Thread==second.Thread&&first.Dpi==second.Dpi&&AreDpiAwarenessContextsEqual(first.Context,second.Context);}
    bool SameGeneration(Host host){return host.Window==parent&&host.Pid==parentPid&&host.Thread==parentThread;}
    void Remember(IntPtr candidate,Host host,ref SafeFileHandle process){own=candidate;parent=host.Window;parentPid=host.Pid;parentThread=host.Thread;if(process!=null){if(parentProcess!=null)parentProcess.Dispose();parentProcess=process;process=null;}}
    static bool Alive(SafeFileHandle handle){return handle!=null&&!handle.IsInvalid&&!handle.IsClosed&&WaitForSingleObject(handle,0)==WaitTimeout;}
    static bool Owned(IntPtr window){uint pid;return window!=IntPtr.Zero&&IsWindow(window)&&GetWindowThreadProcessId(window,out pid)!=0&&pid==(uint)Process.GetCurrentProcess().Id;}
    static bool SameDpi(IntPtr child,IntPtr host){IntPtr a=GetWindowDpiAwarenessContext(child),b=GetWindowDpiAwarenessContext(host);return a!=IntPtr.Zero&&b!=IntPtr.Zero&&AreDpiAwarenessContextsEqual(a,b);}
    static bool Map(IntPtr host,ref Point point){NativeSetLastError(0);int mapped=MapWindowPoints(IntPtr.Zero,host,ref point,1);return mapped!=0||Marshal.GetLastWin32Error()==0;}
    static bool ReadLong(IntPtr window,int index,out long value){NativeSetLastError(0);IntPtr result=GetWindowLongPtr(window,index);value=result.ToInt64();return result!=IntPtr.Zero||Marshal.GetLastWin32Error()==0;}
    static bool WriteLong(IntPtr window,int index,long value){NativeSetLastError(0);IntPtr previous=SetWindowLongPtr(window,index,new IntPtr(value));return previous!=IntPtr.Zero||Marshal.GetLastWin32Error()==0;}
    static bool IsShellHost(IntPtr window) {
      var text=new StringBuilder(256);if(GetClassName(window,text,text.Capacity)<=0||text.ToString()!="Shell_TrayWnd")return false;
      uint pid;if(GetWindowThreadProcessId(window,out pid)==0||pid==0||pid==(uint)Process.GetCurrentProcess().Id)return false;
      using(var process=OpenProcess(0x101000,false,pid)){if(!Alive(process))return false;var image=new StringBuilder(32768);uint size=(uint)image.Capacity;return QueryFullProcessImageName(process,0,image,ref size)&&String.Equals(Path.GetFullPath(image.ToString()),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),StringComparison.OrdinalIgnoreCase);}
    }
    public void Dispose(){if(disposed)return;disposed=true;if(parentProcess!=null){parentProcess.Dispose();parentProcess=null;}}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string name,string title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder name,int count);
    [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")]static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")]static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
    [DllImport("user32.dll",SetLastError=true)]static extern int MapWindowPoints(IntPtr from,IntPtr to,ref Point point,uint count);
    [DllImport("user32.dll",EntryPoint="SetParent",SetLastError=true)]static extern IntPtr NativeSetParent(IntPtr child,IntPtr parent);
    [DllImport("user32.dll",SetLastError=true)]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)]static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
    [DllImport("kernel32.dll",EntryPoint="SetLastError")]static extern void NativeSetLastError(uint error);
    [DllImport("kernel32.dll",SetLastError=true)]static extern SafeFileHandle OpenProcess(uint access,bool inherit,uint process);
    [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(SafeFileHandle process,uint milliseconds);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool QueryFullProcessImageName(SafeFileHandle process,uint flags,StringBuilder image,ref uint count);
  }
}
