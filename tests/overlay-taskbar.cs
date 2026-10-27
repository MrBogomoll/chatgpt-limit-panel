using System;
using System.Collections.Generic;
using System.Linq;
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

internal static class TaskbarNativeTest {
  static int assertions;
  static void Check(bool condition,string message) {assertions++;if(!condition)throw new Exception(message);}
  static bool Near(double a,double b) {return Math.Abs(a-b)<0.01;}
  static TaskbarArea Area(double x,double y,double w,double h) {return new TaskbarArea{X=x,Y=y,Width=w,Height=h};}
  static TaskbarSnapshot Snapshot() {return new TaskbarSnapshot{Bounds=Area(0,1032,1920,48),Scale=1,Visible=true,OccupancyKnown=true,Handle=new IntPtr(123)};}
  static bool Overlap(TaskbarArea a,TaskbarArea b) {return a.X<b.X+b.Width&&a.X+a.Width>b.X&&a.Y<b.Y+b.Height&&a.Y+a.Height>b.Y;}
  static IEnumerable<TextBlock> Text(DependencyObject root) {
    var text=root as TextBlock;if(text!=null)yield return text;
    for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Text(VisualTreeHelper.GetChild(root,i)))yield return child;
  }
  static void Pump() {var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);}
  static void Refresh(TaskbarWindow window) {typeof(TaskbarWindow).GetMethod("RefreshPlacement",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,null);Pump();}
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
  static bool Above(IntPtr above,IntPtr below){for(int i=0;i<100&&below!=IntPtr.Zero;i++){below=GetWindow(below,3);if(below==above)return true;}return false;}

  static void Allocation() {
    var edgeOnly=Snapshot();edgeOnly.Occupied.Add(Area(0,1032,1920,1));edgeOnly.Occupied.Add(Area(0,1079,1920,1));
    Check(TaskbarLayout.Place(edgeOnly,"left").Visible&&TaskbarLayout.Place(edgeOnly,"right").Visible,"one-pixel outer resize edges must not occupy the separate centered two-row footprint");
    var snapshot=Snapshot();snapshot.Occupied.Add(Area(820,1032,300,48));snapshot.Occupied.Add(Area(1640,1032,280,48));
    var left=TaskbarLayout.Place(snapshot,"left");var right=TaskbarLayout.Place(snapshot,"right");
    Check(left.Visible&&right.Visible,"both independently available sides must show");
    Check(left.Bounds.X>=0&&left.Bounds.X+left.Bounds.Width<=820,"left may not cover Start/apps");
    Check(right.Bounds.X>=1120&&right.Bounds.X+right.Bounds.Width<=1640,"right may not cover app buttons or clock");
    foreach(var obstacle in snapshot.Occupied)Check(!Overlap(left.Bounds,obstacle)&&!Overlap(right.Bounds,obstacle),"indicator overlaps a protected region");
    var withWidget=Snapshot();withWidget.Occupied.Add(Area(1700,1032,220,48));withWidget.Occupied.Add(Area(1540,1032,160,48));
    var beforeWidget=TaskbarLayout.Place(withWidget,"right");Check(beforeWidget.Visible&&beforeWidget.Bounds.X+beforeWidget.Bounds.Width<1540,"foreign widget is protected as well as tray");
    var full=Snapshot();full.Occupied.Add(full.Bounds);Check(!TaskbarLayout.Place(full,"right").Visible,"full taskbar must hide");
    var onlyRight=Snapshot();onlyRight.Occupied.Add(Area(0,1032,960,48));Check(!TaskbarLayout.Place(onlyRight,"left").Visible,"left preference must not silently use right half");Check(TaskbarLayout.Place(onlyRight,"right").Visible,"remaining right half is usable");
    var fragmented=Snapshot();fragmented.Occupied.Add(Area(960,1032,400,48));fragmented.Occupied.Add(Area(1380,1032,540,48));Check(!TaskbarLayout.Place(fragmented,"right").Visible,"narrow fragments cannot squeeze percentages");
    var outside=Snapshot();outside.Occupied.Add(Area(1500,0,400,100));Check(TaskbarLayout.Place(outside,"right").Visible,"nonintersecting window is not taskbar occupancy");
    var dpi=Snapshot();dpi.Scale=1.5;dpi.Bounds=Area(-2880,-72,2880,72);dpi.Occupied.Add(Area(-480,-72,480,72));var scaled=TaskbarLayout.Place(dpi,"right");
    Check(scaled.Visible&&Near(scaled.Bounds.Width/1.5,left.Bounds.Width)&&Near(scaled.Bounds.Height/1.5,left.Bounds.Height),"physical footprint must scale at fractional DPI");
    Check(scaled.Bounds.X<0&&scaled.Bounds.Y<0&&scaled.Bounds.X+scaled.Bounds.Width<-480,"negative monitor coordinates must remain physical pixels");
    var merged=Snapshot();merged.Occupied.Add(Area(1400,1032,300,48));merged.Occupied.Add(Area(1300,1032,200,48));var mergedPlacement=TaskbarLayout.Place(merged,"right");foreach(var block in merged.Occupied)Check(!Overlap(mergedPlacement.Bounds,block),"overlapping obstacles must merge");
  }
  static void FailClosed() {
    Check(!TaskbarLayout.Place(null,"right").Visible,"missing snapshot must hide");
    foreach(string flag in new[]{"Visible","OccupancyKnown","AutoHidden","Fullscreen"}) {var snapshot=Snapshot();typeof(TaskbarSnapshot).GetField(flag).SetValue(snapshot,flag=="AutoHidden"||flag=="Fullscreen");Check(!TaskbarLayout.Place(snapshot,"right").Visible,"unsafe flag "+flag+" must hide");}
    foreach(double scale in new[]{0,-1,Double.NaN,Double.PositiveInfinity}) {var snapshot=Snapshot();snapshot.Scale=scale;Check(!TaskbarLayout.Place(snapshot,"right").Visible,"invalid scale must hide");}
    var invalid=Snapshot();invalid.Bounds.Width=Double.NaN;Check(!TaskbarLayout.Place(invalid,"right").Visible,"invalid taskbar bounds must hide");
    invalid=Snapshot();invalid.Occupied.Add(Area(Double.NaN,1032,10,48));Check(!TaskbarLayout.Place(invalid,"right").Visible,"invalid occupied geometry must hide");
    invalid=Snapshot();invalid.Occupied=null;Check(!TaskbarLayout.Place(invalid,"right").Visible,"missing occupancy list must hide");
    invalid=Snapshot();invalid.Bounds=Area(0,0,48,1000);Check(!TaskbarLayout.Place(invalid,"right").Visible,"vertical bar is unsupported");
    invalid=Snapshot();invalid.Bounds.Height=20;Check(!TaskbarLayout.Place(invalid,"right").Visible,"two readable rows must fit height");
    Check(!TaskbarLayout.Place(Snapshot(),"invalid").Visible,"invalid persisted side must hide rather than guess");
  }
  static void NativeWidgetClassification() {
    foreach(string name in new[]{"ToolbarWindow32","NetWorxDeskband","UnknownPaintedStatus"})Check(TaskbarNative.ProtectNativeWindow(name,true,true),"an Explorer-process painted leaf must protect its rectangle: "+name);
    foreach(string name in new[]{"ReBarWindow32","MSTaskSwWClass","MSTaskListWClass","Windows.UI.Composition.DesktopWindowContentBridge","Windows.UI.Input.InputSite.WindowClass","XamlExplorerHostIslandWindow"}) {
      Check(!TaskbarNative.ProtectNativeWindow(name,true,true),"known shell layout HWND may span unused taskbar space: "+name);
      Check(TaskbarNative.ProtectNativeWindow(name,false,true),"foreign widget cannot borrow a layout class exemption: "+name);
    }
    Check(TaskbarNative.ProtectNativeWindow("TrayNotifyWnd",true,false)&&TaskbarNative.ProtectNativeWindow("Start",true,false),"known interactive shell regions stay protected even with children");
    var snapshot=Snapshot();var paintedWidget=Area(1560,1032,360,48);
    if(TaskbarNative.ProtectNativeWindow("ToolbarWindow32",true,true))snapshot.Occupied.Add(paintedWidget);
    var placement=TaskbarLayout.Place(snapshot,"right");Check(placement.Visible&&placement.Bounds.X+placement.Bounds.Width<1560,"classic painted taskbar widget is excluded from candidate placement");
  }
  static void AutoHideGeometry() {
    TaskbarArea monitor=Area(0,0,2560,1440);TaskbarArea revealed=Area(0,1368,2560,72);
    Check(TaskbarNative.IsTaskbarFullyRevealed(revealed,monitor,1.5),"fully revealed taskbar has a complete readable row");
    Check(!TaskbarNative.IsAutoHideHidden(true,revealed,monitor,1.5),"enabled auto-hide may show indicator while fully revealed");
    foreach(TaskbarArea transitioning in new[]{Area(0,1400,2560,72),Area(0,1438,2560,72),Area(0,1438,2560,2),Area(0,1369,2560,72)}) {
      Check(!TaskbarNative.IsTaskbarFullyRevealed(transitioning,monitor,1.5),"offscreen or sliding row is not fully revealed");
      Check(TaskbarNative.IsAutoHideHidden(true,transitioning,monitor,1.5),"auto-hide transition must suppress indicator");
    }
    Check(!TaskbarNative.IsTaskbarFullyRevealed(Area(0,1368,2500,72),monitor,1.5),"partial-width primary geometry is unsupported");
    Check(!TaskbarNative.IsTaskbarFullyRevealed(Area(0,1400,2560,40),monitor,1.5),"row must fit both percentages at current DPI");
    Check(!TaskbarNative.IsFullscreenBounds(monitor,monitor,true,0x00C40000L),"ordinary maximized framed app must not suppress a revealed auto-hide taskbar");
    Check(TaskbarNative.IsFullscreenBounds(monitor,monitor,false,0),"borderless monitor-sized app is fullscreen");
    Check(TaskbarNative.IsFullscreenBounds(monitor,monitor,true,0),"borderless fullscreen remains fullscreen even when maximized");
    Check(!TaskbarNative.IsFullscreenBounds(Area(0,0,1280,800),monitor,false,0),"a normal borderless window is not fullscreen");
  }
  static void OwnWindow() {
    int reads=0;var snapshot=Snapshot();snapshot.Bounds=Area(-20000,-20000,1920,48);bool fail=false;
    var window=new TaskbarWindow(delegate {reads++;if(fail)throw new InvalidOperationException("fake geometry failed");return snapshot;});
    try {
      Check(!window.Enabled&&!window.IsVisible&&reads==0,"default off must not show or read taskbar");
      window.ApplyRows("100%","30%","normal","warning",false);
      window.Configure(false,"right",ThemePalette.Dark);Check(!window.IsVisible&&reads==0,"disabled updates must not probe or show");
      IntPtr foreground=GetForegroundWindow();window.Configure(true,"right",ThemePalette.Dark);Refresh(window);
      Check(window.Enabled&&window.IsVisible,"enabled verified offscreen gap shows own window");
      Check(GetForegroundWindow()==foreground,"indicator must not steal focus");
      var labels=Text(window).ToArray();Check(labels.Length==2&&labels[0].Text=="100%"&&labels[1].Text=="30%","only ordered quota percentages may be visible");
      foreach(var label in labels){Rect labelBounds=label.TransformToAncestor(window).TransformBounds(new Rect(0,0,label.ActualWidth,label.ActualHeight));Check(labelBounds.Width>0&&labelBounds.Height>0&&labelBounds.Top>=-0.01&&labelBounds.Bottom<=window.ActualHeight+0.01,"both rows must fit actual native viewport");}
      IntPtr handle=new WindowInteropHelper(window).Handle;long style=GetWindowLongPtr(handle,-20).ToInt64();Check((style&0x08000000)!=0&&(style&0x80)!=0,"own window must be NOACTIVATE tool window");
      IntPtr owner=GetWindowLongPtr(handle,-8);uint ownerProcess=0;if(owner!=IntPtr.Zero)GetWindowThreadProcessId(owner,out ownerProcess);Check(owner==IntPtr.Zero||ownerProcess==(uint)System.Diagnostics.Process.GetCurrentProcess().Id,"indicator must have no foreign parent or owner");
      Check(!window.ShowInTaskbar&&!window.ShowActivated&&!window.Focusable,"indicator must retain nonactivating WPF behavior");
      Exception automationFailure=null;int cachedTexts=0;var complete=new ManualResetEvent(false);
      ThreadPool.QueueUserWorkItem(delegate {try{var elements=TaskbarNative.ReadAutomationElements(handle);for(int i=0;i<elements.Count;i++)if(elements[i].Cached.ControlType==ControlType.Text)cachedTexts++;}catch(Exception error){automationFailure=error;}finally{complete.Set();}});
      var automationWait=System.Diagnostics.Stopwatch.StartNew();while(!complete.WaitOne(0)&&automationWait.ElapsedMilliseconds<3000){Pump();Thread.Sleep(5);}
      Check(complete.WaitOne(0)&&automationFailure==null&&cachedTexts>=2,"background UIA must enumerate own cached descendants from a live root: "+automationFailure);
      complete.Dispose();
      window.ContextMenu=new ContextMenu{Visibility=Visibility.Collapsed};window.ContextMenu.IsOpen=true;
      var cover=new Window{Left=-21000,Top=-21000,Width=32,Height=32,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,Topmost=true};
      try{
        cover.Show();Pump();IntPtr coverHandle=new WindowInteropHelper(cover).Handle;TaskbarNative.SetWindowPos(coverHandle,new IntPtr(-1),0,0,0,0,0x0013);Check(Above(coverHandle,handle),"isolated cover fixture must be above indicator");Refresh(window);Check(Above(coverHandle,handle),"tracker may not raise indicator while its own context menu is open");
        var visited=new List<IntPtr>();Check(TaskbarNative.ForEachWindowAbove(handle,delegate(IntPtr item){visited.Add(item);})&&visited.Contains(coverHandle)&&!visited.Contains(handle),"occupancy enumeration visits own fixture above boundary only");
        TaskbarNative.SetWindowPos(coverHandle,handle,0,0,0,0,0x0013);visited.Clear();Check(TaskbarNative.ForEachWindowAbove(handle,delegate(IntPtr item){visited.Add(item);})&&!visited.Contains(coverHandle),"background windows below taskbar boundary cannot block free gaps");
        window.ContextMenu.IsOpen=false;window.IsOtherMenuOpen=delegate{return true;};TaskbarNative.SetWindowPos(coverHandle,new IntPtr(-1),0,0,0,0,0x0013);Refresh(window);Check(Above(coverHandle,handle),"other own tray/main menu must also prevent tracker raising");
        window.IsOtherMenuOpen=delegate{return false;};Refresh(window);Check(!Above(coverHandle,handle),"indicator may resume normal taskbar layering after all own menus close");
      }finally{cover.Close();window.ContextMenu.IsOpen=false;window.ContextMenu=null;}
      double freshOpacity=labels[0].Opacity;window.ApplyRows("8%","15%","critical","warning",true);labels=Text(window).ToArray();Check(labels[0].Text=="8%"&&labels[1].Text=="15%"&&labels[0].Opacity<freshOpacity,"stale data must retain values and dim both rows");
      Check(labels[1].Opacity<1,"weekly stale row must also dim");
      window.Configure(true,"left",ThemePalette.Light);Refresh(window);Check(window.IsVisible,"theme and side may update without changing values");Check(Text(window).First().Text=="8%","theme change must preserve percentages");
      window.ApplyRows("5h 75%","missing","normal","unknown",false);Check(Text(window).All(item=>item.Text=="—"),"labels and invalid data cannot leak into percentage-only UI");
      snapshot.OccupancyKnown=false;Refresh(window);Check(!window.IsVisible&&window.PlacementStatus!="visible","unknown occupancy hides existing window");
      snapshot.OccupancyKnown=true;snapshot.AutoHidden=true;Refresh(window);Check(!window.IsVisible,"autohide hides own window");snapshot.AutoHidden=false;snapshot.Fullscreen=true;Refresh(window);Check(!window.IsVisible,"fullscreen hides own window");
      snapshot.Fullscreen=false;fail=true;Refresh(window);Check(!window.IsVisible,"provider exception hides instead of retaining last placement");
      fail=false;window.Configure(false,"right",ThemePalette.Dark);Check(!window.Enabled&&!window.IsVisible,"disabling stops visible indicator");
      window.Dispose();window.Configure(true,"right",ThemePalette.Dark);Check(!window.Enabled&&!window.IsVisible,"disposed indicator cannot be revived");
    } finally {window.Dispose();}
  }
  static void SnapshotLifecycle() {
    long now=0;bool safe=true;int scans=0;var started=new ManualResetEvent(false);var release=new ManualResetEvent(false);
    var source=new TaskbarSnapshotSource(delegate{Interlocked.Increment(ref scans);started.Set();release.WaitOne();return Snapshot();},delegate{return Interlocked.Read(ref now);},delegate(TaskbarSnapshot value){return safe;});
    try {
      Check(source.Read()==null,"first background read must hide before geometry is known");Check(started.WaitOne(2000),"background geometry scan must start");
      for(int i=0;i<20;i++)Check(source.Read()==null,"pending scan must not expose unmeasured geometry");Check(scans==1,"pending geometry cannot queue unbounded workers");
      release.Set();var wait=System.Diagnostics.Stopwatch.StartNew();TaskbarSnapshot value=null;while((value=source.Read())==null&&wait.ElapsedMilliseconds<1000)Thread.Sleep(5);Check(value!=null,"completed fresh geometry becomes available");
      safe=false;Check(source.Read()==null,"fast fullscreen/autohide guard must reject a cached safe snapshot immediately");safe=true;
      Interlocked.Exchange(ref now,1700);Check(source.Read()==null,"expired geometry must never be reused while refreshing");
      source.Dispose();Check(source.Read()==null,"disposed reader cannot provide geometry");
    }finally{release.Set();source.Dispose();}
    var slowStarted=new ManualResetEvent(false);var slowRelease=new ManualResetEvent(false);long slowNow=0;
    var slow=new TaskbarSnapshotSource(delegate{slowStarted.Set();slowRelease.WaitOne();return Snapshot();},delegate{return Interlocked.Read(ref slowNow);},delegate(TaskbarSnapshot value){return true;});
    try{Check(slow.Read()==null&&slowStarted.WaitOne(2000),"slow scan starts without blocking UI");Interlocked.Exchange(ref slowNow,1300);slowRelease.Set();Thread.Sleep(30);Check(slow.Read()==null,"over-budget acquisition must not become a fresh successful snapshot");}finally{slowRelease.Set();slow.Dispose();}
  }
  [STAThread] static int Main() {try {Allocation();FailClosed();NativeWidgetClassification();AutoHideGeometry();SnapshotLifecycle();OwnWindow();Console.WriteLine("PASS taskbar allocation and offscreen indicator ("+assertions+" assertions)");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
