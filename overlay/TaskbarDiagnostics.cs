using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace QuotaOverlay {
  // Owns only bounded metadata records. The lock is never held during native
  // queries, serialization, disk IO or the worker's rate-limit wait.
  internal sealed class TaskbarDiagnosticSpool {
    sealed class Pending {internal string Key;internal IntPtr Widget;internal Dictionary<string,object> State;}
    const int Capacity=8,MinimumIntervalMilliseconds=200;
    readonly object gate=new object();
    readonly List<Pending> pending=new List<Pending>();
    readonly Action<string,IntPtr,Dictionary<string,object>> write;
    readonly Action<Action> schedule;
    readonly Func<long> now;
    readonly Action<int> delay;
    readonly Stopwatch clock=Stopwatch.StartNew();
    bool running;
    long lastStarted=-MinimumIntervalMilliseconds,dropped,coalesced;

    internal TaskbarDiagnosticSpool(Action<string,IntPtr,Dictionary<string,object>> write,Action<Action> schedule=null,Func<long> now=null,Action<int> delay=null) {
      if(write==null)throw new ArgumentNullException("write");
      this.write=write;
      this.schedule=schedule??delegate(Action action){if(!ThreadPool.QueueUserWorkItem(delegate{action();}))throw new InvalidOperationException("Diagnostic worker unavailable");};
      this.now=now??delegate{return clock.ElapsedMilliseconds;};
      this.delay=delay??Thread.Sleep;
    }
    internal void Enqueue(string key,IntPtr widget,Dictionary<string,object> state) {
      try {
        if(String.IsNullOrWhiteSpace(key))return;
        var copy=state==null?new Dictionary<string,object>():new Dictionary<string,object>(state);
        copy["diagnosticQueuedAt"]=DateTime.UtcNow.ToString("o");
        var item=new Pending{Key=key,Widget=widget,State=copy};
        lock(gate) {
          if(pending.Count>0&&String.Equals(pending[pending.Count-1].Key,key,StringComparison.Ordinal)){pending[pending.Count-1]=item;coalesced++;}
          else {if(pending.Count>=Capacity){pending.RemoveAt(0);dropped++;}pending.Add(item);}
          if(!running)ScheduleLocked();
        }
      }catch{}
    }
    void ScheduleLocked() {
      running=true;
      try{schedule(Drain);}catch{running=false;}
    }
    void Drain() {
      try {
        while(true) {
          Pending item;long droppedBefore,coalescedBefore;int remaining;
          lock(gate) {
            if(pending.Count==0)return;
            item=pending[0];pending.RemoveAt(0);droppedBefore=dropped;coalescedBefore=coalesced;remaining=pending.Count;
          }
          long elapsed=now()-lastStarted;
          if(elapsed>=0&&elapsed<MinimumIntervalMilliseconds)delay((int)(MinimumIntervalMilliseconds-elapsed));
          lastStarted=now();
          try {
            item.State["diagnosticWorkerAt"]=DateTime.UtcNow.ToString("o");
            item.State["diagnosticQueue"]=new {dropped=droppedBefore,coalesced=coalescedBefore,pending=remaining};
            write(item.Key,item.Widget,item.State);
          }catch{}
        }
      }catch{}
      finally {
        lock(gate){running=false;if(pending.Count>0)ScheduleLocked();}
      }
    }
  }
  // Temporary metadata-only diagnostics for the actual widget. No window titles,
  // accessibility queries, classification state, hooks or input are involved.
  internal static class TaskbarDiagnostics {
    internal static bool Enabled {get{return Environment.GetEnvironmentVariable("QUOTA_PANEL_DIAGNOSTICS")=="1";}}
    const int MaximumFileBytes=1024*1024,MaximumLineBytes=64*1024;
    static readonly object gate=new object();
    static string lastKey;
    static long sequence;
    static readonly TaskbarDiagnosticSpool spool=new TaskbarDiagnosticSpool(delegate(string key,IntPtr widget,Dictionary<string,object> state){state["desktop"]=Desktop(widget);Write(key,state);});

    internal static void Enqueue(string key,IntPtr widget,Dictionary<string,object> state) {if(Enabled)spool.Enqueue(key,widget,state);}

    internal static void Write(string key,object payload) {
      lock(gate) {
        if(String.Equals(key,lastKey,StringComparison.Ordinal))return;
        // Remember failed attempts too: unavailable storage must not cause an IO
        // retry on every GUI tick. A later state transition will try again.
        lastKey=key;
        try {
          string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChatGPTQuotaOverlay");
          string path=Path.Combine(folder,"taskbar-debug.jsonl"),previous=Path.Combine(folder,"taskbar-debug.previous.jsonl");
          var serializer=new JavaScriptSerializer{MaxJsonLength=MaximumFileBytes,RecursionLimit=48};
          long currentSequence=++sequence;
          string line=serializer.Serialize(new {time=DateTime.UtcNow.ToString("o"),process=Process.GetCurrentProcess().Id,sequence=currentSequence,key=key,payload=payload})+Environment.NewLine;
          byte[] bytes=Encoding.UTF8.GetBytes(line);
          if(bytes.Length>MaximumLineBytes) {
            line=serializer.Serialize(new {time=DateTime.UtcNow.ToString("o"),process=Process.GetCurrentProcess().Id,sequence=currentSequence,key=key,payload=new {omitted=true,reason="payload-too-large",bytes=bytes.Length}})+Environment.NewLine;
            bytes=Encoding.UTF8.GetBytes(line);
            if(bytes.Length>MaximumLineBytes)return;
          }
          Directory.CreateDirectory(folder);
          long size=File.Exists(path)?new FileInfo(path).Length:0;
          if(size+bytes.Length>MaximumFileBytes) {
            // Only these two reserved diagnostic files are touched.
            if(File.Exists(path))File.Copy(path,previous,true);
            using(var reset=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.ReadWrite)){}
          }
          using(var stream=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.ReadWrite))stream.Write(bytes,0,bytes.Length);
        } catch { }
      }
    }

    internal static object Window(IntPtr hwnd) {
      var data=new Dictionary<string,object>();data["handle"]=Hex(hwnd);
      try {
        bool exists=hwnd!=IntPtr.Zero&&IsWindow(hwnd);data["exists"]=exists;
        if(!exists)return data;
        string cls=Class(hwnd);uint pid;uint thread=GetWindowThreadProcessId(hwnd,out pid);
        IntPtr owner=GetWindow(hwnd,4);SetLastError(0);IntPtr instance=Long(hwnd,-6);int instanceError=Marshal.GetLastWin32Error();
        RECT rect;bool hasRect=GetWindowRect(hwnd,out rect);
        data["visible"]=IsWindowVisible(hwnd);data["iconic"]=IsIconic(hwnd);data["class"]=cls;
        data["processId"]=pid;data["threadId"]=thread;data["rect"]=hasRect?(object)Area(rect):null;
        data["style"]=unchecked((uint)Long(hwnd,-16).ToInt64()).ToString("X8");
        data["extendedStyle"]=unchecked((uint)Long(hwnd,-20).ToInt64()).ToString("X8");
        uint ownerPid=0;if(owner!=IntPtr.Zero)GetWindowThreadProcessId(owner,out ownerPid);
        data["owner"]=new {handle=Hex(owner),@class=Class(owner),processId=ownerPid};
        data["instance"]=Hex(instance);
        data["instanceReadError"]=instance==IntPtr.Zero?instanceError:0;
        IntPtr bar=FindWindow("Shell_TrayWnd",null);data["aboveTaskbar"]=Above(hwnd,bar);
        try {
          RECT frame;int cloaked;
          if(DwmGetWindowAttribute(hwnd,9,out frame,Marshal.SizeOf(typeof(RECT)))==0)data["extendedFrame"]=Area(frame);
          if(DwmGetWindowAttributeInt(hwnd,14,out cloaked,4)==0)data["cloaked"]=cloaked;
        } catch { }
        try {
          using(var process=Process.GetProcessById((int)pid)) {
            data["processName"]=process.ProcessName;
            try {data["processPath"]=process.MainModule.FileName;}catch(Exception error){data["processPathError"]=error.GetType().Name;}
            if(cls=="#32770"&&instance!=IntPtr.Zero) {
              try {
                int count=0;object matching=null;
                foreach(ProcessModule module in process.Modules) {
                  if(++count>256)break;
                  if(module.BaseAddress==instance){matching=new {name=module.ModuleName,path=module.FileName,address=Hex(module.BaseAddress),size=module.ModuleMemorySize};break;}
                }
                data["moduleForInstance"]=matching;data["modulesExamined"]=Math.Min(count,256);
              } catch(Exception error){data["moduleError"]=error.GetType().Name;}
            }
          }
        } catch(Exception error){data["processError"]=error.GetType().Name;}
      } catch(Exception error){data["error"]=error.GetType().Name;}
      return data;
    }

    internal static object Desktop(IntPtr widget) {
      var data=new Dictionary<string,object>();
      try {
        IntPtr bar=FindWindow("Shell_TrayWnd",null),foreground=GetForegroundWindow();
        data["widget"]=Window(widget);data["taskbar"]=Window(bar);data["foreground"]=Window(foreground);
        data["widgetAboveTaskbar"]=Above(widget,bar);data["taskbarAboveWidget"]=Above(bar,widget);
        data["foregroundAboveWidget"]=Above(foreground,widget);
        var menus=new List<object>();var overlappingAboveBar=new List<object>();
        RECT widgetRect;bool hasWidgetRect=GetWindowRect(widget,out widgetRect);
        int examined=0;bool limit=false;
        EnumWindows(delegate(IntPtr hwnd,IntPtr ignored) {
          if(examined++>=128){limit=true;return false;}
          if(!IsWindowVisible(hwnd))return true;
          string cls=Class(hwnd);
          if(menus.Count<8&&(cls=="#32768"||cls=="#32770"||cls=="Xaml_WindowedPopupClass"))
            menus.Add(new {window=Window(hwnd),aboveWidget=Above(hwnd,widget),zIndex=examined-1});
          if(overlappingAboveBar.Count<8&&hwnd!=widget&&hwnd!=bar&&hasWidgetRect&&Above(hwnd,bar)==true) {
            RECT other;
            if(GetWindowRect(hwnd,out other)&&Overlap(widgetRect,other))
              overlappingAboveBar.Add(new {window=Window(hwnd),aboveWidget=Above(hwnd,widget),zIndex=examined-1});
          }
          return true;
        },IntPtr.Zero);
        data["menuCandidates"]=menus;data["overlappingAboveTaskbar"]=overlappingAboveBar;
        data["windowsExamined"]=Math.Min(examined,128);data["windowLimitReached"]=limit;
      } catch(Exception error){data["error"]=error.GetType().Name;}
      return data;
    }

    // GW_HWNDPREV walks towards the front of the same native Z order. A null
    // result means the query could not establish a bounded comparison.
    internal static bool AboveTaskbar(IntPtr widget){return Above(widget,FindWindow("Shell_TrayWnd",null))==true;}
    static bool? Above(IntPtr candidate,IntPtr reference) {
      if(candidate==IntPtr.Zero||reference==IntPtr.Zero||!IsWindow(candidate)||!IsWindow(reference))return null;
      if(candidate==reference)return false;
      IntPtr current=reference;
      for(int i=0;i<512;i++) {
        current=GetWindow(current,3);
        if(current==IntPtr.Zero)return false;
        if(current==candidate)return true;
      }
      return null;
    }
    static bool Overlap(RECT a,RECT b){return a.Left<b.Right&&a.Right>b.Left&&a.Top<b.Bottom&&a.Bottom>b.Top;}
    static object Area(RECT value){return new {left=value.Left,top=value.Top,right=value.Right,bottom=value.Bottom,width=value.Right-value.Left,height=value.Bottom-value.Top};}
    static string Hex(IntPtr value){return unchecked((ulong)value.ToInt64()).ToString("X");}
    static string Class(IntPtr hwnd){if(hwnd==IntPtr.Zero)return null;var text=new StringBuilder(256);return GetClassName(hwnd,text,text.Capacity)>0?text.ToString():null;}
    static IntPtr Long(IntPtr hwnd,int index){return IntPtr.Size==8?GetWindowLongPtr(hwnd,index):new IntPtr(GetWindowLong(hwnd,index));}
    [StructLayout(LayoutKind.Sequential)]struct RECT {public int Left,Top,Right,Bottom;}
    delegate bool EnumWindowProc(IntPtr hwnd,IntPtr parameter);
    [DllImport("user32.dll")]static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")]static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
    [DllImport("user32.dll")]static extern bool EnumWindows(EnumWindowProc callback,IntPtr parameter);
    [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr hwnd,uint command);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="FindWindowW")]static extern IntPtr FindWindow(string cls,string name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="GetClassNameW")]static extern int GetClassName(IntPtr hwnd,StringBuilder value,int length);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)]static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW",SetLastError=true)]static extern int GetWindowLong(IntPtr hwnd,int index);
    [DllImport("kernel32.dll")]static extern void SetLastError(uint error);
    [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out RECT value,int size);
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]static extern int DwmGetWindowAttributeInt(IntPtr hwnd,int attribute,out int value,int size);
  }
}
