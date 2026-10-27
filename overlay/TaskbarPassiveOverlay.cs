using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace QuotaOverlay {
  // This identifies a narrowly guarded passive canvas, not invisible pixels.
  // GetLayeredWindowAttributes flags=0 does not establish global transparency.
  // Callers may place their own widget BELOW it; this alone never grants an
  // occupancy exemption or permission to cover NVIDIA HUDs or menus.
  internal static class TaskbarPassiveOverlay {
    const long RequiredStyle=0x98000000L,ForbiddenStyle=0x40C40000L;
    const long RequiredExtended=0x000800A8L,ForbiddenExtended=0x00040000L;
    const long IdentityLifetimeMilliseconds=1000;
    const int MaximumIdentities=128,MaximumZOrderSteps=512;
    const uint ProcessQueryLimitedInformation=0x1000,Synchronize=0x100000,WaitTimeout=258;
    static readonly object gate=new object();
    static readonly Stopwatch clock=Stopwatch.StartNew();
    static readonly Dictionary<uint,Identity> identities=new Dictionary<uint,Identity>();
    sealed class Identity:IDisposable {
      internal long CheckedAt;internal bool Trusted;internal SafeFileHandle Process;
      public void Dispose(){if(Process!=null)Process.Dispose();}
    }
    struct Metadata {internal uint Process,Thread;internal string Name;internal long Style,Extended;internal bool Unowned;}
    [StructLayout(LayoutKind.Sequential)]struct Rect {internal int Left,Top,Right,Bottom;}

    internal static bool IsPassiveCanvasMetadata(string name,long style,long exStyle,bool unowned,bool sameForegroundProcess,bool trustedImage) {
      return String.Equals(name,"CEF-OSC-WIDGET",StringComparison.Ordinal)&&
        (style&RequiredStyle)==RequiredStyle&&(style&ForbiddenStyle)==0&&
        (exStyle&RequiredExtended)==RequiredExtended&&(exStyle&ForbiddenExtended)==0&&
        unowned&&!sameForegroundProcess&&trustedImage;
    }
    internal static bool IsTrustedImagePath(string image,string programFiles) {
      try {
        if(!LocalAbsolute(image)||!LocalAbsolute(programFiles))return false;
        string expected=Path.Combine(Path.GetFullPath(programFiles),"NVIDIA Corporation","NVIDIA App","CEF","NVIDIA Overlay.exe");
        return String.Equals(Path.GetFullPath(image),expected,StringComparison.OrdinalIgnoreCase);
      }catch(ArgumentException){}catch(NotSupportedException){}catch(PathTooLongException){}
      return false;
    }
    static bool LocalAbsolute(string value) {return !String.IsNullOrWhiteSpace(value)&&value.Length>=3&&Char.IsLetter(value[0])&&value[1]==':'&&(value[2]=='\\'||value[2]=='/');}

    internal static bool IsPassiveCanvas(IntPtr window) {
      try {
        Metadata first;uint foreground;
        if(!ReadMetadata(window,out first)||!ForegroundProcess(out foreground))return false;
        if(!IsPassiveCanvasMetadata(first.Name,first.Style,first.Extended,first.Unowned,foreground!=0&&first.Process==foreground,true))return false;
        if(!TrustedProcess(first.Process))return false;
        // Revalidate native state after the identity query. A reused HWND or a
        // newly activated NVIDIA UI cannot inherit an earlier canvas decision.
        Metadata current;
        if(!ReadMetadata(window,out current)||current.Process!=first.Process||current.Thread!=first.Thread||!ForegroundProcess(out foreground))return false;
        return IsPassiveCanvasMetadata(current.Name,current.Style,current.Extended,current.Unowned,foreground!=0&&current.Process==foreground,true);
      }catch{return false;}
    }
    static bool ForegroundProcess(out uint process) {
      process=0;IntPtr foreground=GetForegroundWindow();
      // Windows normally returns no foreground during activation transitions.
      // The underlay contract still protects the canvas when that occurs.
      return foreground==IntPtr.Zero||(GetWindowThreadProcessId(foreground,out process)!=0&&process!=0);
    }
    static bool ReadMetadata(IntPtr window,out Metadata result) {
      result=new Metadata();
      if(window==IntPtr.Zero||!IsWindow(window)||!IsWindowVisible(window))return false;
      int cloaked;if(DwmGetWindowAttribute(window,14,out cloaked,4)!=0||cloaked!=0)return false;
      result.Thread=GetWindowThreadProcessId(window,out result.Process);if(result.Thread==0||result.Process==0)return false;
      var name=new StringBuilder(256);if(GetClassName(window,name,name.Capacity)<=0)return false;result.Name=name.ToString();
      if(!TryLong(window,-16,out result.Style)||!TryLong(window,-20,out result.Extended))return false;
      Rect bounds;if(!GetWindowRect(window,out bounds)||bounds.Right<=bounds.Left||bounds.Bottom<=bounds.Top)return false;
      NativeSetLastError(0);IntPtr owner=GetWindow(window,4);int ownerError=Marshal.GetLastWin32Error();
      if(owner==IntPtr.Zero&&ownerError!=0)return false;result.Unowned=owner==IntPtr.Zero;
      return true;
    }
    static bool TryLong(IntPtr window,int index,out long value) {
      NativeSetLastError(0);IntPtr result=GetWindowLongPtr(window,index);int error=Marshal.GetLastWin32Error();value=result.ToInt64();
      return result!=IntPtr.Zero||error==0;
    }
    static bool TrustedProcess(uint process) {
      if(process==0)return false;
      lock(gate) {
        long now=clock.ElapsedMilliseconds;Identity identity;
        if(identities.TryGetValue(process,out identity)) {
          bool alive=identity.Process!=null&&!identity.Process.IsInvalid&&!identity.Process.IsClosed&&WaitForSingleObject(identity.Process,0)==WaitTimeout;
          if(now-identity.CheckedAt<IdentityLifetimeMilliseconds&&(identity.Process==null||alive))return alive&&identity.Trusted;
          identities.Remove(process);identity.Dispose();
        }
        Prune(now);
        identity=new Identity{CheckedAt=now};
        SafeFileHandle opened=OpenProcess(ProcessQueryLimitedInformation|Synchronize,false,process);
        if(opened!=null&&!opened.IsInvalid) {
          identity.Process=opened;
          var image=new StringBuilder(32768);uint length=(uint)image.Capacity;
          identity.Trusted=QueryFullProcessImageName(opened,0,image,ref length)&&length>0&&WaitForSingleObject(opened,0)==WaitTimeout&&IsTrustedImagePath(image.ToString(),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        }else if(opened!=null)opened.Dispose();
        identities.Add(process,identity);return identity.Trusted;
      }
    }
    static void Prune(long now) {
      var expired=new List<uint>();uint oldest=0;long oldestAt=Int64.MaxValue;
      foreach(var entry in identities) {
        Identity identity=entry.Value;
        if(now-identity.CheckedAt>=IdentityLifetimeMilliseconds||(identity.Process!=null&&(identity.Process.IsInvalid||identity.Process.IsClosed||WaitForSingleObject(identity.Process,0)!=WaitTimeout)))expired.Add(entry.Key);
        if(identity.CheckedAt<oldestAt){oldestAt=identity.CheckedAt;oldest=entry.Key;}
      }
      foreach(uint process in expired){Identity identity=identities[process];identities.Remove(process);identity.Dispose();}
      if(identities.Count>=MaximumIdentities){Identity identity;if(identities.TryGetValue(oldest,out identity)){identities.Remove(oldest);identity.Dispose();}}
    }
    // A false result includes unknown/error/limit cases. No caller may use an
    // unknown order as evidence that its widget remains below a protected HUD.
    internal static bool IsAbove(IntPtr candidate,IntPtr reference) {
      try {
        if(candidate==IntPtr.Zero||reference==IntPtr.Zero||candidate==reference||!IsWindow(candidate)||!IsWindow(reference))return false;
        IntPtr current=reference;
        for(int i=0;i<MaximumZOrderSteps;i++) {
          current=GetWindow(current,3);if(current==IntPtr.Zero)return false;
          if(current==candidate)return IsWindow(candidate)&&IsWindow(reference);
        }
      }catch{}
      return false;
    }

    [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",SetLastError=true)]static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="GetClassNameW")]static extern int GetClassName(IntPtr window,StringBuilder name,int count);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)]static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
    [DllImport("kernel32.dll",EntryPoint="SetLastError")]static extern void NativeSetLastError(uint error);
    [DllImport("kernel32.dll",SetLastError=true)]static extern SafeFileHandle OpenProcess(uint access,bool inherit,uint process);
    [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(SafeFileHandle handle,uint milliseconds);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(SafeFileHandle process,uint flags,StringBuilder image,ref uint length);
  }
}
