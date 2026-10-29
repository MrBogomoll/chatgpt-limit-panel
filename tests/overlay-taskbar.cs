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
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out TaskbarNative.Rect rect);
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
    var vertical=Snapshot();vertical.Bounds=Area(0,0,72,1440);vertical.Scale=1.5;
    vertical.Occupied.Add(Area(0,0,72,820));vertical.Occupied.Add(Area(0,1170,72,270));
    var afterApps=TaskbarLayout.Place(vertical,"left");
    Check(afterApps.Visible&&afterApps.Bounds.Y>820&&afterApps.Bounds.Y+afterApps.Bounds.Height<1170,"vertical Start-side placement must find the real gap after a long top-aligned application group, even past the monitor midpoint");
    vertical.Occupied[0]=Area(0,0,72,1120);vertical.Occupied[1]=Area(0,1124,72,316);
    Check(!TaskbarLayout.Place(vertical,"left").Visible,"a genuinely full vertical bar must hide");
    vertical.Occupied[0]=Area(0,0,72,820);vertical.Occupied[1]=Area(0,1170,72,270);
    Check(TaskbarLayout.Place(vertical,"left").Visible,"moving the same number of occupied rectangles must immediately restore a newly available vertical gap");
    var manual=Snapshot();manual.Occupied.Add(Area(0,1032,600,48));manual.Occupied.Add(Area(1500,1032,420,48));
    var moved=TaskbarLayout.Place(manual,"right",0.5);Check(moved.Visible&&Near(moved.Bounds.X,936),"manual positioning can use free space across the midpoint");
    moved=TaskbarLayout.Place(manual,"right",0.95);Check(moved.Visible&&moved.Bounds.X+moved.Bounds.Width<1500,"dragging toward the tray snaps to a free slot without covering controls");
    moved=TaskbarLayout.Place(vertical,"left",0.5);Check(moved.Visible&&moved.Bounds.Y>820,"vertical manual positioning respects actual obstacles");
    Check(!TaskbarLayout.Place(full,"left",0.5).Visible,"manual positioning never exposes a full taskbar");
    moved=TaskbarLayout.Place(manual,"right",0.333);Check(moved.Visible&&moved.Bounds.X==Math.Round(moved.Bounds.X),"persisted fractional placement resolves to whole physical pixels accepted by the native child attachment");
  }
  static void FailClosed() {
    Check(!TaskbarLayout.Place(null,"right").Visible,"missing snapshot must hide");
    foreach(string flag in new[]{"Visible","OccupancyKnown","AutoHidden","Fullscreen"}) {var snapshot=Snapshot();typeof(TaskbarSnapshot).GetField(flag).SetValue(snapshot,flag=="AutoHidden"||flag=="Fullscreen");Check(!TaskbarLayout.Place(snapshot,"right").Visible,"unsafe flag "+flag+" must hide");}
    foreach(double scale in new[]{0,-1,Double.NaN,Double.PositiveInfinity}) {var snapshot=Snapshot();snapshot.Scale=scale;Check(!TaskbarLayout.Place(snapshot,"right").Visible,"invalid scale must hide");}
    var invalid=Snapshot();invalid.Bounds.Width=Double.NaN;Check(!TaskbarLayout.Place(invalid,"right").Visible,"invalid taskbar bounds must hide");
    invalid=Snapshot();invalid.Occupied.Add(Area(Double.NaN,1032,10,48));Check(!TaskbarLayout.Place(invalid,"right").Visible,"invalid occupied geometry must hide");
    invalid=Snapshot();invalid.Occupied=null;Check(!TaskbarLayout.Place(invalid,"right").Visible,"missing occupancy list must hide");
    invalid=Snapshot();invalid.Bounds=Area(0,0,48,1000);Check(TaskbarLayout.Place(invalid,"right").Visible,"a readable verified vertical bar is supported");
    invalid=Snapshot();invalid.Bounds.Height=20;Check(!TaskbarLayout.Place(invalid,"right").Visible,"two readable rows must fit height");
    Check(!TaskbarLayout.Place(Snapshot(),"invalid").Visible,"invalid persisted side must hide rather than guess");
  }
  static void NativeWidgetClassification() {
    Check(TaskbarNative.RedundantLegacyLayoutPeer(ControlType.Custom,"MSTaskSwWClass",true,true),"an empty legacy layout peer must not mask gaps already measured from the modern taskbar buttons");
    Check(!TaskbarNative.RedundantLegacyLayoutPeer(ControlType.Custom,"MSTaskSwWClass",true,false),"legacy-only or unreadable app buttons must retain conservative occupancy");
    Check(!TaskbarNative.RedundantLegacyLayoutPeer(ControlType.Custom,"MSTaskSwWClass",false,true),"a foreign window cannot borrow the shell layout exemption");
    Check(!TaskbarNative.RedundantLegacyLayoutPeer(ControlType.Custom,"NetWorxDeskband",true,true),"painted widgets remain protected even inside Explorer");
    var legacyAreas=new List<TaskbarArea>{Area(2488,85,80,801)};
    Check(TaskbarNative.MatchesLegacyLayoutArea(Area(2490,83,80,801),legacyAreas,1.5),"virtual Start11 layout peer matches its measured native container despite a two-pixel provider inset");
    Check(!TaskbarNative.MatchesLegacyLayoutArea(Area(2490,83,80,500),legacyAreas,1.5),"an arbitrary custom region inside the container must remain occupied");
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
    foreach(var edge in new[]{Area(0,0,2560,72),Area(0,0,72,1440),Area(2488,0,72,1440)})Check(TaskbarNative.IsTaskbarFullyRevealed(edge,monitor,1.5),"top and both vertical monitor edges are fully revealed");
    foreach(var sliding in new[]{Area(0,-1,2560,72),Area(-1,0,72,1440),Area(2489,0,72,1440),Area(0,1,72,1440)})Check(!TaskbarNative.IsTaskbarFullyRevealed(sliding,monitor,1.5),"partially hidden or misaligned non-bottom edge must hide");
    Check(!TaskbarNative.IsAutoHideHidden(true,revealed,monitor,1.5),"enabled auto-hide may show indicator while fully revealed");
    foreach(var sliding in new[]{Area(0,1438,2560,72),Area(0,-70,2560,72),Area(-70,0,72,1440),Area(2558,0,72,1440)}) {
      Check(TaskbarNative.IsChildTaskbarGeometry(sliding,monitor,1.5,true),"native child may follow a full-sized auto-hidden parent on every edge");
      Check(!TaskbarNative.IsChildTaskbarGeometry(sliding,monitor,1.5,false),"offscreen parent is rejected when auto-hide is disabled");
    }
    foreach(var invalid in new[]{Area(0,1438,2560,2),Area(1,1438,2560,72),Area(0,500,2560,72),Area(0,1441,2560,72)})Check(!TaskbarNative.IsChildTaskbarGeometry(invalid,monitor,1.5,true),"invalid or floating hidden parent remains rejected");
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
        Exception recoveryFailure=null;bool recoveredRoot=false,protectedUnreadable=false;var recoveryDone=new ManualResetEvent(false);
        ThreadPool.QueueUserWorkItem(delegate {try {
          var live=TaskbarNative.ReadAutomationElements(handle);var protectedAreas=new List<TaskbarArea>();
          var recovered=TaskbarNative.ReadTaskbarAutomationElements(coverHandle,new[]{handle},protectedAreas,delegate(IntPtr root){if(root==coverHandle)throw new ElementNotAvailableException();return live;});
          recoveredRoot=recovered!=null&&recovered.Count==live.Count&&protectedAreas.Count==0;
          var readable=TaskbarNative.ReadTaskbarAutomationElements(handle,new[]{coverHandle},protectedAreas,delegate(IntPtr root){if(root==coverHandle)throw new ElementNotAvailableException();return live;});
          protectedUnreadable=readable!=null&&protectedAreas.Count==1&&TaskbarLayout.Valid(protectedAreas[0]);
        }catch(Exception error){recoveryFailure=error;}finally{recoveryDone.Set();}});
        var recoveryWait=System.Diagnostics.Stopwatch.StartNew();while(!recoveryDone.WaitOne(0)&&recoveryWait.ElapsedMilliseconds<3000){Pump();Thread.Sleep(5);}
        Check(recoveryDone.WaitOne(0)&&recoveryFailure==null,"independent native accessibility subtrees must be queried safely: "+recoveryFailure);
        Check(recoveredRoot,"a failed wrapper provider must recover fresh descendants from the real native subtree");
        Check(protectedUnreadable,"an unreadable concrete subtree must reserve its full current native bounds");recoveryDone.Dispose();
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
  static void PassiveAnchorRenderer() {
    var sample=Snapshot();sample.Bounds=Area(-20000,-20000,1920,48);TaskbarSnapshot current=sample;
    bool menu=false;long now=0;int calls=0;string mode="deny-before";
    bool firstShown=false,firstTransparent=false,postTransparent=false,postPreserved=false,heldSnapshot=true,heldPreserved=true;
    TaskbarWindow window=null;
    Func<TaskbarSnapshot,TaskbarArea,bool,IntPtr?> anchor=delegate(TaskbarSnapshot value,TaskbarArea bounds,bool preserve) {
      calls++;
      if(calls==1){firstShown=window.IsVisible;firstTransparent=Near(window.Opacity,0);}
      if(mode=="deny-before")return null;
      if(mode=="deny-after"&&calls==2){postTransparent=Near(window.Opacity,0);postPreserved=preserve;return null;}
      if(menu){heldSnapshot=heldSnapshot&&Object.ReferenceEquals(value,sample);heldPreserved=heldPreserved&&preserve;}
      return new IntPtr(-1);
    };
    window=new TaskbarWindow(delegate{return current;},delegate{return menu;},delegate{return "passive-evidence";},delegate{return now;},anchor);
    try {
      IntPtr foreground=GetForegroundWindow();
      window.Configure(true,"right",ThemePalette.Dark);
      // Drive the refresh phases explicitly; Pump must not add timer ticks.
      ((DispatcherTimer)typeof(TaskbarWindow).GetField("timer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window)).Stop();
      Check(calls==1&&firstShown&&firstTransparent,"first anchor proof runs only after the own HWND exists at zero opacity");
      Check(!window.IsVisible&&Near(window.Opacity,0)&&window.PlacementStatus=="overlay-order-unverified","missing pre-placement order proof hides without exposing pixels");
      calls=0;mode="deny-after";Refresh(window);
      Check(calls==2&&postTransparent&&postPreserved,"post-placement proof preserves order and runs before the first opaque frame");
      Check(!window.IsVisible&&Near(window.Opacity,0)&&window.PlacementStatus=="overlay-order-changed","a failed post-placement order proof conceals the still-transparent own window");
      calls=0;mode="allow";Refresh(window);
      Check(calls==2&&window.IsVisible&&Near(window.Opacity,1),"successful pre/post order proofs allow the own window to become opaque");
      menu=true;current=null;now=1000;calls=0;Refresh(window);
      Check(calls==2&&heldSnapshot&&heldPreserved&&window.IsVisible&&window.PlacementStatus=="menu-held","menu retention verifies the leased snapshot and preserves native order when fresh geometry is absent");
      now=15000;calls=0;Refresh(window);
      Check(calls==2&&heldSnapshot&&heldPreserved&&window.IsVisible,"an already active menu keeps using leased geometry beyond the accessibility cache lifetime");
      mode="deny-before";Refresh(window);
      Check(!window.IsVisible&&window.PlacementStatus=="overlay-order-unverified","loss of underlay-order proof revokes a visible menu-held widget");
      mode="allow";calls=0;Refresh(window);
      Check(calls==0&&!window.IsVisible,"a hidden widget cannot be revived while its menu is still active");
      menu=false;current=sample;now=16000;Refresh(window);
      Check(window.IsVisible&&Near(window.Opacity,1),"fresh normal geometry can restore placement after the menu ends");
      Check(foreground==GetForegroundWindow(),"transparent order checks and all recovery paths preserve the real foreground");
    }finally{window.Dispose();}
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
  static void MenuWhileProviderStalls() {
    long now=0;bool menu=false,block=false,safe=true;string fingerprint="native-1";
    var sample=Snapshot();sample.Bounds=Area(-20000,-20000,1920,48);
    var entered=new ManualResetEvent(false);var release=new ManualResetEvent(false);
    var cache=new TaskbarSnapshotSource(delegate{if(block){entered.Set();release.WaitOne();}return sample;},delegate{return now;},delegate{return true;});
    var window=new TaskbarWindow(cache.Read,delegate{return menu;},delegate(TaskbarSnapshot value,TaskbarArea bounds){return safe?fingerprint:null;},delegate{return now;});
    var cover=new Window{Left=-21000,Top=-21000,Width=32,Height=32,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,Topmost=true};
    try {
      window.Configure(true,"right",ThemePalette.Dark);
      var wait=System.Diagnostics.Stopwatch.StartNew();while(!window.IsVisible&&wait.ElapsedMilliseconds<2000){Refresh(window);Thread.Sleep(5);}
      Check(window.IsVisible,"menu regression begins from a freshly verified displayed footprint");
      IntPtr handle=new WindowInteropHelper(window).Handle;TaskbarNative.Rect first;GetWindowRect(handle,out first);IntPtr foreground=GetForegroundWindow();
      menu=true;safe=false;now=100;Refresh(window);
      Check(!window.IsVisible,"a changed native guard must reject even a still TTL-valid accessibility placement during the menu");
      menu=false;safe=true;now=200;Refresh(window);Check(window.IsVisible,"a verified normal frame can rearm after the unsafe menu closes");
      menu=true;block=true;now=500;Refresh(window);Check(entered.WaitOne(1000),"the next accessibility scan must be stalled by the simulated modal menu");
      cover.Show();Pump();IntPtr coverHandle=new WindowInteropHelper(cover).Handle;TaskbarNative.SetWindowPos(coverHandle,new IntPtr(-1),0,0,0,0,0x0013);
      foreach(long moment in new long[]{1700,15000}) {
        now=moment;Refresh(window);window.Configure(true,"right",ThemePalette.Light);Refresh(window);
        TaskbarNative.Rect held;GetWindowRect(handle,out held);
        Check(window.IsVisible&&window.PlacementStatus=="menu-held","a recognized menu retains the already visible widget beyond the UIA cache lifetime");
        Check(first.Left==held.Left&&first.Top==held.Top&&first.Right==held.Right&&first.Bottom==held.Bottom,"a stalled provider may not allocate or move the retained footprint");
        Check(Above(coverHandle,handle)&&GetForegroundWindow()==foreground,"retention keeps native menu layering and foreground unchanged");
      }
      safe=false;Refresh(window);Check(!window.IsVisible,"changed native evidence immediately revokes a retained footprint");
      safe=true;Refresh(window);Check(!window.IsVisible,"a revoked lease cannot revive a hidden widget while its menu remains open");
      menu=false;block=false;release.Set();Refresh(window);
      wait.Restart();while(!window.IsVisible&&wait.ElapsedMilliseconds<2000){Refresh(window);Thread.Sleep(5);}
      Check(window.IsVisible,"fresh accessibility data restores normal placement after the menu closes");
      release.Reset();entered.Reset();block=true;menu=true;now=15500;Refresh(window);Check(entered.WaitOne(1000),"the provider can stall for a second independent menu");now=18000;Refresh(window);
      fingerprint="native-2";Refresh(window);Check(!window.IsVisible,"an app-window or taskbar fingerprint change revokes the exact retained position");
      window.Configure(false,"right",ThemePalette.Dark);Check(!window.IsVisible&&!window.Enabled,"disabling remains authoritative during a modal menu");
    }finally{release.Set();window.Dispose();cover.Close();cache.Dispose();}
  }
  static void SnapshotMeasurementRace() {
    long now=0;int calls=0;bool reuse=false;
    var first=Snapshot();var second=Snapshot();var entered=new ManualResetEvent(false);var release=new ManualResetEvent(false);
    var source=new TaskbarSnapshotSource(delegate{int call=Interlocked.Increment(ref calls);if(call==1)return first;entered.Set();release.WaitOne();return reuse?first:second;},delegate{return Interlocked.Read(ref now);},delegate{return true;});
    var current=typeof(TaskbarSnapshotSource).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic);
    try {
      TaskbarSnapshotSource.Measurement initial;source.Read(out initial);
      var wait=System.Diagnostics.Stopwatch.StartNew();while(!Object.ReferenceEquals(current.GetValue(source),first)&&wait.ElapsedMilliseconds<1000)Thread.Sleep(5);
      Check(Object.ReferenceEquals(current.GetValue(source),first),"measurement race fixture must acquire its first snapshot");
      Interlocked.Exchange(ref now,400);TaskbarSnapshotSource.Measurement oldReceipt;var captured=source.Read(out oldReceipt);
      Check(Object.ReferenceEquals(captured,first)&&entered.WaitOne(1000),"a read must capture fresh A while a second scan is pending");
      Interlocked.Exchange(ref now,450);release.Set();wait.Restart();while(!Object.ReferenceEquals(current.GetValue(source),second)&&wait.ElapsedMilliseconds<1000)Thread.Sleep(5);
      Check(Object.ReferenceEquals(current.GetValue(source),second),"the second snapshot must publish before calculating captured A age");
      Check(source.SnapshotAge(oldReceipt)==450,"publication of B must preserve the captured A measurement timestamp");
      TaskbarSnapshotSource.Measurement secondReceipt;Check(Object.ReferenceEquals(source.Read(out secondReceipt),second)&&source.SnapshotAge(secondReceipt)==50,"a new read must capture B independently of A");
      release.Reset();entered.Reset();reuse=true;Interlocked.Exchange(ref now,800);TaskbarSnapshotSource.Measurement stillSecond;source.Read(out stillSecond);Check(entered.WaitOne(1000),"the scanner may return a previously used snapshot object");Interlocked.Exchange(ref now,850);release.Set();wait.Restart();while(!Object.ReferenceEquals(current.GetValue(source),first)&&wait.ElapsedMilliseconds<1000)Thread.Sleep(5);
      TaskbarSnapshotSource.Measurement sameObjectReceipt;Check(Object.ReferenceEquals(source.Read(out sameObjectReceipt),first)&&source.SnapshotAge(sameObjectReceipt)==50,"republishing an object captures the new measurement independently");
      Check(source.SnapshotAge(oldReceipt)==850,"republishing the same object in the same generation must not renew a prior receipt");
      Interlocked.Exchange(ref now,1701);Check(source.SnapshotAge(oldReceipt)>1600,"an old receipt must truly expire without renewal by later publications");
      source.Invalidate();Check(source.SnapshotAge(secondReceipt)>1600,"explicit invalidation must revoke previously returned measurements");
      release.Reset();entered.Reset();reuse=true;Interlocked.Exchange(ref now,1800);TaskbarSnapshotSource.Measurement empty;source.Read(out empty);Check(entered.WaitOne(1000),"a fresh generation must acquire independently");release.Set();wait.Restart();while(!Object.ReferenceEquals(current.GetValue(source),first)&&wait.ElapsedMilliseconds<1000)Thread.Sleep(5);
      TaskbarSnapshotSource.Measurement reusedReceipt;Check(Object.ReferenceEquals(source.Read(out reusedReceipt),first)&&source.SnapshotAge(reusedReceipt)==0,"reusing the same object must yield a distinct fresh measurement");
      Check(source.SnapshotAge(oldReceipt)>1600,"republishing the same snapshot object must not renew its previous receipt");
      Interlocked.Exchange(ref now,1799);Check(source.SnapshotAge(reusedReceipt)>1600,"a clock moving behind receipt acquisition must fail closed");
      Interlocked.Exchange(ref now,1800);source.Dispose();Check(source.SnapshotAge(reusedReceipt)>1600,"disposing the reader must revoke its last receipt");
      Check(source.SnapshotAge((TaskbarSnapshotSource.Measurement)null)>1600,"missing receipts must fail closed");
    }finally{release.Set();source.Dispose();}
  }
  [STAThread] static int Main() {try {Allocation();FailClosed();NativeWidgetClassification();AutoHideGeometry();SnapshotLifecycle();SnapshotMeasurementRace();OwnWindow();MenuWhileProviderStalls();PassiveAnchorRenderer();Console.WriteLine("PASS taskbar allocation and offscreen indicator ("+assertions+" assertions)");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
