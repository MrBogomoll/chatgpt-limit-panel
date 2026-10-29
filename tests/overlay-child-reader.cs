using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using QuotaOverlay;

internal static class ChildReaderTest {
  static int assertions;
  static readonly List<IntPtr> windows=new List<IntPtr>();
  static readonly WndProc procedure=DefWindowProc;
  static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
  static IntPtr Window(IntPtr parent,int x,int y,int width,int height) {
    uint style=parent==IntPtr.Zero?0x80000000U:0x40000000U;
    IntPtr result=CreateWindowEx(parent==IntPtr.Zero?0x080800A0U:0x08000020U,"STATIC",null,style,x,y,width,height,parent,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
    Check(result!=IntPtr.Zero,"own transparent offscreen fixture is created");windows.Add(result);
    if(parent==IntPtr.Zero)Check(SetLayeredWindowAttributes(result,0,0,2),"own fixture stays transparent");
    Check(SetWindowPos(result,parent==IntPtr.Zero?new IntPtr(-1):IntPtr.Zero,x,y,width,height,0x0050),"own fixture is positioned without activation");return result;
  }
  static TaskbarArea Area(IntPtr window){NativeRect rect;Check(GetWindowRect(window,out rect),"read own native fixture rectangle");return new TaskbarArea{X=rect.Left,Y=rect.Top,Width=rect.Right-rect.Left,Height=rect.Bottom-rect.Top};}
  static string Fingerprint(TaskbarSnapshot sample,TaskbarArea footprint,IntPtr own,bool childMode) {
    return TaskbarNative.LeaseFingerprintCore(sample,footprint,own,delegate{return true;},delegate{return false;},0,childMode);
  }
  static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);}
  static IList<AutomationElement> Automation(IntPtr root) {
    IList<AutomationElement> result=null;Exception failure=null;var complete=new ManualResetEvent(false);
    ThreadPool.QueueUserWorkItem(delegate{try{var read=TaskbarNative.ReadAutomationElements(root);var list=new List<AutomationElement>();for(int i=0;i<read.Count;i++)list.Add(read[i]);result=list;}catch(Exception error){failure=error;}finally{complete.Set();}});
    var clock=Stopwatch.StartNew();while(!complete.WaitOne(0)&&clock.ElapsedMilliseconds<5000){Pump();Thread.Sleep(2);}
    Check(complete.WaitOne(0)&&failure==null&&result!=null,"background own accessibility collection completes: "+(failure==null?"":failure.GetType().Name));complete.Dispose();return result;
  }
  static IntPtr Landmark(string name,IntPtr parent,int x,int width){
    var registration=new WindowClass{Procedure=procedure,Instance=GetModuleHandle(null),ClassName=name};Check(RegisterClass(ref registration)!=0,"register only own landmark class "+name);
    IntPtr result=CreateWindowEx(0x08000020,name,null,0x50000000U,x,0,width,48,parent,IntPtr.Zero,registration.Instance,IntPtr.Zero);
    Check(result!=IntPtr.Zero,"create own native landmark "+name);windows.Add(result);return result;
  }
  static void NativeLease() {
    IntPtr foreground=GetForegroundWindow();
    IntPtr bar=Window(IntPtr.Zero,-27000,-27000,600,48),own=Window(bar,420,0,72,48),text=Window(own,3,3,20,12);
    TaskbarArea bounds=Area(bar),footprint=Area(own);
    var sample=new TaskbarSnapshot{Handle=bar,Bounds=bounds,Scale=GetDpiForWindow(bar)/96.0,Visible=true,OccupancyKnown=true};
    string baseline=Fingerprint(sample,footprint,own,true);
    Check(baseline!=null,"attached child and its native descendants must not occupy their own allocation");
    Check(Fingerprint(sample,footprint,own,false)==null,"legacy detached collection still treats native children as protected occupancy");
    IntPtr cover=Window(IntPtr.Zero,-27000,-27000,600,48);
    Check(Fingerprint(sample,footprint,own,true)==baseline,"covering top-level game-like window does not change child taskbar allocation evidence");
    Check(Fingerprint(sample,footprint,own,false)==null,"legacy overlay still rejects a covering top-level window");
    IntPtr widget=Window(bar,20,2,40,40);
    string before=Fingerprint(sample,footprint,own,true);Check(before!=null&&before!=baseline,"another taskbar child remains in evidence even with the same process ID");
    Check(SetWindowPos(bar,IntPtr.Zero,-27000,-26960,600,48,0x0014),"slide only the own taskbar fixture");
    sample.Bounds=Area(bar);footprint=Area(own);
    Check(Fingerprint(sample,footprint,own,true)==before,"moving parent and sibling widgets together preserves local native evidence");
    Check(SetWindowPos(bar,IntPtr.Zero,-27000,-27000,600,48,0x0014),"restore own taskbar fixture");
    sample.Bounds=Area(bar);footprint=Area(own);
    Check(SetWindowPos(widget,IntPtr.Zero,40,2,40,40,0x0014),"move only own foreign-widget fixture");
    string moved=Fingerprint(sample,footprint,own,true);Check(moved!=null&&moved!=before,"changed native widget geometry revokes old retention evidence");
    Check(SetWindowPos(widget,IntPtr.Zero,430,2,40,40,0x0014),"move only own widget fixture into the protected footprint");
    Check(Fingerprint(sample,footprint,own,true)==null&&TaskbarNative.LastFingerprintReason=="protected-child-overlap","an overlapping sibling widget must not be exempted by shared PID");
    ShowWindow(widget,0);Check(Fingerprint(sample,footprint,own,true)==baseline,"removing the conflicting native widget restores complete evidence");
    Check(TaskbarNative.IsVerifiedOwnChild(own,bar)&&TaskbarNative.IsOwnNativeSubtree(text,own),"only our actual direct child and same-process descendants prove ownership");
    Check(!TaskbarNative.IsOwnNativeSubtree(widget,own)&&!TaskbarNative.IsVerifiedOwnChild(cover,bar),"same-process sibling and unattached top-level windows cannot borrow ownership");
    Check(TaskbarNative.ParentSnapshotMatches(sample,own,bar,true),"child parent geometry and DPI proof succeeds for the real own fixture");
    var invalid=new TaskbarSnapshot{Handle=bar,Bounds=bounds,Scale=sample.Scale+0.5};
    Check(!TaskbarNative.ParentSnapshotMatches(invalid,own,bar,true),"changed DPI invalidates parent proof");invalid.Scale=sample.Scale;invalid.Bounds.X++;
    Check(!TaskbarNative.ParentSnapshotMatches(invalid,own,bar,true),"stale parent coordinates invalidate proof");
    Check(!TaskbarNative.ParentSnapshotMatches(sample,own,cover,true),"a changed parent identity invalidates proof");
    ShowWindow(bar,0);Check(!TaskbarNative.ParentSnapshotMatches(sample,own,bar,true),"hidden parent cannot provide display evidence");ShowWindow(bar,8);
    ShowWindow(bar,6);Check(!TaskbarNative.ParentSnapshotMatches(sample,own,bar,true),"minimized parent cannot provide display evidence");ShowWindow(bar,4);
    Check(!TaskbarNative.CanDisplaySnapshot(sample,own,true),"production child snapshot guard rejects a fake shell parent");
    Check(GetForegroundWindow()==foreground,"child allocation and fixture creation preserve actual foreground");
    SnapshotAndAutomation(bar,own,widget,bounds,sample.Scale);
  }
  static void SnapshotAndAutomation(IntPtr bar,IntPtr own,IntPtr foreign,TaskbarArea bounds,double scale) {
    Landmark("Start",bar,0,18);Landmark("TrayNotifyWnd",bar,560,40);ShowWindow(foreign,8);
    Check(SetWindowPos(foreign,IntPtr.Zero,20,2,40,40,0x0014),"place own foreign-control fixture away from child allocation");
    var elements=Automation(bar);int ownPeers=0,otherPeers=0;
    foreach(var element in elements){var cached=element.Cached;if(TaskbarNative.IsOwnAutomationElement(element,own,bar))ownPeers++;else otherPeers++;}
    Check(ownPeers>0&&otherPeers>0,"cached UIA distinguishes own child peers and other taskbar controls");
    Func<TaskbarSnapshot> fresh=delegate{return new TaskbarSnapshot{Handle=bar,Bounds=bounds,Scale=scale,Visible=true};};
    Func<IntPtr,IList<IntPtr>,IList<TaskbarArea>,IList<AutomationElement>> reader=delegate{return elements;};
    var known=TaskbarNative.CompleteSnapshot(fresh(),own,true,delegate{return bar;},reader);
    Check(known.OccupancyKnown,"child-aware collector acquires complete native and UIA occupancy");
    var placement=TaskbarLayout.Place(known,"right");Check(placement.Visible,"the child's own native/UIA content does not remove all safe space");
    TaskbarArea ownBounds=Area(own);foreach(TaskbarArea obstacle in known.Occupied)Check(!(obstacle.X==ownBounds.X&&obstacle.Y==ownBounds.Y&&obstacle.Width==ownBounds.Width&&obstacle.Height==ownBounds.Height),"own root HWND is absent from occupied regions");
    var missing=TaskbarNative.CompleteSnapshot(fresh(),own,true,delegate{return bar;},delegate{return null;});
    Check(!missing.OccupancyKnown&&!TaskbarLayout.Place(missing,"right").Visible,"unknown accessibility cannot create a fresh visible allocation");
    var changed=TaskbarNative.CompleteSnapshot(fresh(),own,true,delegate{return IntPtr.Zero;},reader);Check(!changed.OccupancyKnown,"changed shell identity cancels acquisition");
    VirtualPeers(bar);
  }
  static void VirtualPeers(IntPtr parent) {
    var window=new System.Windows.Window{Left=-27000,Top=-27000,Width=48,Height=32,ShowActivated=false,ShowInTaskbar=false,Focusable=false,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Opacity=0,Content=new TextBlock{Text="92%"}};
    try {
      window.Show();Pump();IntPtr own=new WindowInteropHelper(window).Handle;
      long style=TaskbarNative.GetWindowLongPtr(own,-16).ToInt64();TaskbarNative.SetWindowLongPtr(own,-16,new IntPtr((style|0x40000000L)&~0x80000000L));
      SetParent(own,parent);Check(GetParent(own)==parent,"attach only own WPF peer fixture to own native parent");
      Check(SetWindowPos(own,IntPtr.Zero,420,0,72,48,0x0010),"position own WPF peer fixture in parent-client coordinates");window.Opacity=1;Pump();
      var peers=Automation(own);bool virtualText=false;
      foreach(var element in peers)if(element.Cached.ControlType==ControlType.Text&&element.Cached.NativeWindowHandle==0){virtualText=true;Check(TaskbarNative.IsOwnAutomationElement(element,own,parent),"our HWND-less WPF text peer is excluded only under verified child ownership and bounds");}
      Check(virtualText,"actual WPF text supplies a cache-only virtual peer regression");
    }finally{window.Close();}
  }
  [STAThread]static int Main(){try{NativeLease();Console.WriteLine("PASS native child reader ("+assertions+" assertions)");return 0;}catch(Exception error){Console.Error.WriteLine(error.GetType().Name+": "+error.Message);return 1;}finally{for(int i=windows.Count-1;i>=0;i--)DestroyWindow(windows[i]);}}
  [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int Left,Top,Right,Bottom;}
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateWindowEx(uint extended,string name,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr parameter);
  [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr window);
  [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
  [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr anchor,int x,int y,int width,int height,uint flags);
  [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
  [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
  [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr window);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr window);
  [DllImport("user32.dll")]static extern IntPtr SetParent(IntPtr window,IntPtr parent);
  delegate IntPtr WndProc(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct WindowClass {public uint Style;public WndProc Procedure;public int ClassExtra,WindowExtra;public IntPtr Instance,Icon,Cursor,Background;public string MenuName,ClassName;}
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern ushort RegisterClass(ref WindowClass registration);
  [DllImport("user32.dll")]static extern IntPtr DefWindowProc(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
}
