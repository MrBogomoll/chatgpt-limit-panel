using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace QuotaOverlay {
  internal sealed class TaskbarWindow:Window,IDisposable {
    readonly Func<TaskbarSnapshot> readSnapshot;
    readonly TaskbarSnapshotSource source;
    readonly DispatcherTimer timer;
    readonly Stopwatch placementClock=Stopwatch.StartNew();
    readonly TaskbarMenuLease menuLease=new TaskbarMenuLease();
    readonly Func<bool> readTransient;
    readonly Func<TaskbarSnapshot,TaskbarArea,string> readLeaseFingerprint;
    readonly Func<TaskbarSnapshot,TaskbarArea,bool,IntPtr?> readPlacementAnchor;
    readonly Func<long> placementMilliseconds;
    TaskbarSnapshot leaseSnapshot;
    TaskbarPlacement lastShown;
    bool wasTransient;
    long recoveryUntil;
    string diagnosticKey,diagnosticFingerprint;
    object diagnosticProof,diagnosticBaselineProof;
    TaskbarSnapshot diagnosticCandidate;
    bool diagnosticTransient;
    long diagnosticAge;
    readonly TextBlock upper,lower;
    readonly Border surface;
    ThemePalette palette=ThemePalette.Dark;
    string side="right",upperTone="unknown",lowerTone="unknown";
    bool stale,disposed,closed;
    int backgroundTransparency=50;
    IntPtr hwnd;
    public bool Enabled {get;private set;}
    public string PlacementStatus {get;private set;}
    public bool IsVertical {get;private set;}
    public Func<bool> IsOtherMenuOpen {get;set;}

    public TaskbarWindow(Func<TaskbarSnapshot> readSnapshot=null,Func<bool> readTransient=null,Func<TaskbarSnapshot,TaskbarArea,string> readLeaseFingerprint=null,Func<long> nowMilliseconds=null,Func<TaskbarSnapshot,TaskbarArea,bool,IntPtr?> readPlacementAnchor=null) {
      if(readSnapshot==null){source=new TaskbarSnapshotSource();this.readSnapshot=source.Read;}else this.readSnapshot=readSnapshot;
      this.readTransient=readTransient??delegate{IntPtr capture;return TaskbarTransient.IsActive(out capture);};
      this.readLeaseFingerprint=readLeaseFingerprint??delegate(TaskbarSnapshot value,TaskbarArea bounds){return TaskbarNative.LeaseFingerprint(value,bounds,hwnd);};
      this.readPlacementAnchor=readPlacementAnchor??delegate(TaskbarSnapshot value,TaskbarArea bounds,bool preserve){return source==null?(IntPtr?)new IntPtr(-1):TaskbarNative.PlacementAnchor(value,bounds,hwnd,preserve);};
      placementMilliseconds=nowMilliseconds??delegate{return placementClock.ElapsedMilliseconds;};
      Width=TaskbarLayout.Width;Height=TaskbarLayout.Height;WindowStartupLocation=WindowStartupLocation.Manual;
      WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
      ShowInTaskbar=false;ShowActivated=false;Focusable=false;UseLayoutRounding=true;SnapsToDevicePixels=true;
      upper=Label();lower=Label();var rows=new Grid();rows.RowDefinitions.Add(new RowDefinition());rows.RowDefinitions.Add(new RowDefinition());
      Grid.SetRow(lower,1);rows.Children.Add(upper);rows.Children.Add(lower);
      surface=new Border{Width=TaskbarLayout.Width,Height=TaskbarLayout.Height,Padding=new Thickness(3,0,3,0),CornerRadius=new CornerRadius(3),Child=rows};
      Content=new Viewbox{Stretch=Stretch.Uniform,Child=surface};
      PlacementStatus="disabled";
      timer=new DispatcherTimer(DispatcherPriority.Background,Dispatcher);timer.Interval=TimeSpan.FromMilliseconds(200);timer.Tick+=delegate{RefreshPlacement();};
      SourceInitialized+=delegate {
        hwnd=new WindowInteropHelper(this).Handle;
        long style=TaskbarNative.GetWindowLongPtr(hwnd,-20).ToInt64();TaskbarNative.SetWindowLongPtr(hwnd,-20,new IntPtr(style|0x08000000L|0x80L));
        HwndSource.FromHwnd(hwnd).AddHook(WindowMessage);
        if(source!=null)source.OwnWindow=hwnd;
      };
      Closed+=delegate{closed=true;Stop();};
      ApplyPalette();
    }
    static TextBlock Label() {return new TextBlock{Text="—",FontFamily=new FontFamily("Segoe UI"),FontSize=12,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,TextAlignment=TextAlignment.Center,Focusable=false};}
    static Brush Color(string value) {var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));brush.Freeze();return brush;}
    static string Percentage(string value) {
      if(String.IsNullOrWhiteSpace(value)||!value.EndsWith("%",StringComparison.Ordinal))return "—";
      double numeric;if(!Double.TryParse(value.Substring(0,value.Length-1).Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out numeric)||!TaskbarLayout.Finite(numeric)||numeric<0||numeric>100)return "—";
      return Math.Round(numeric).ToString("0",CultureInfo.InvariantCulture)+"%";
    }
    public void Configure(bool enabled,string side,ThemePalette palette,int transparency=50) {
      if(disposed||closed)return;
      if(this.side!=side)InvalidateMenuLease();
      this.side=side;this.palette=palette??ThemePalette.Dark;backgroundTransparency=Math.Max(0,Math.Min(100,transparency));ApplyPalette();Enabled=enabled;
      if(!enabled){timer.Stop();if(source!=null)source.Invalidate();InvalidateMenuLease();Conceal("disabled");return;}
      timer.Start();RefreshPlacement();
    }
    public void ApplyRows(string upper,string lower,string upperTone,string lowerTone,bool stale) {
      if(disposed||closed)return;
      this.upper.Text=Percentage(upper);this.lower.Text=Percentage(lower);this.upperTone=upperTone;this.lowerTone=lowerTone;this.stale=stale;ApplyPalette();
    }
    void ApplyPalette() {Background=TaskbarAppearance.InputSurface(backgroundTransparency);surface.Background=Color(palette.Background(backgroundTransparency));upper.Foreground=Color(palette.Tone(upperTone));lower.Foreground=Color(palette.Tone(lowerTone));upper.Opacity=lower.Opacity=stale?0.55:1;}
    void Conceal(string reason) {PlacementStatus=reason;if(IsVisible)Hide();TracePlacement(null);}
    void TracePlacement(string message) {
      if(source==null||!TaskbarDiagnostics.Enabled)return;
      try {
        bool above=TaskbarDiagnostics.AboveTaskbar(hwnd);
        string key=PlacementStatus+"|"+diagnosticTransient+"|"+menuLease.LastReason+"|"+above+"|"+message+"|"+(diagnosticTransient?(diagnosticCandidate==null?"null":"candidate"):"");
        if(key==diagnosticKey)return;diagnosticKey=key;
        TaskbarDiagnostics.Enqueue(key,hwnd,new Dictionary<string,object>{{"status",PlacementStatus},{"message",message},{"visible",IsVisible},{"opacity",Opacity},{"transient",diagnosticTransient},{"candidateKnown",diagnosticCandidate!=null&&diagnosticCandidate.OccupancyKnown},{"candidateAge",diagnosticAge},{"fingerprint",diagnosticFingerprint},{"proof",diagnosticProof},{"baseline",diagnosticBaselineProof},{"lease",menuLease.SnapshotDebug(placementMilliseconds())},{"source",source.SnapshotDebug(diagnosticCandidate)}});
      }catch{}
    }
    void InvalidateMenuLease(){menuLease.Invalidate();leaseSnapshot=null;lastShown=null;wasTransient=false;recoveryUntil=0;}
    void RefreshPlacement() {
      if(disposed||closed||!Enabled)return;
      try {
        TaskbarSnapshotSource.Measurement measurement=null;
        TaskbarSnapshot snapshot=source==null?readSnapshot():source.Read(out measurement);diagnosticCandidate=snapshot;if(snapshot!=null&&TaskbarLayout.Valid(snapshot.Bounds))IsVertical=snapshot.Bounds.Height>snapshot.Bounds.Width;
        TaskbarPlacement placement=TaskbarLayout.Place(snapshot,side);
        long now=placementMilliseconds();bool transient=readTransient();diagnosticTransient=transient;
        if(!transient&&wasTransient)recoveryUntil=now+1600;
        if(transient)recoveryUntil=0;
        wasTransient=transient;
        bool hold=transient||(!placement.Visible&&now<recoveryUntil);
        string fingerprint=hold&&leaseSnapshot!=null&&lastShown!=null?readLeaseFingerprint(leaseSnapshot,lastShown.Bounds):null;
        if(hold){diagnosticFingerprint=fingerprint;diagnosticProof=new {reason=TaskbarNative.LastFingerprintReason,parts=TaskbarNative.LastFingerprintParts};}
        TaskbarPlacement retained;
        bool retainedPosition=menuLease.TryHold(hold,IsVisible,fingerprint!=null,fingerprint,now,out retained);
        if(hold&&!retainedPosition){Conceal("menu-unverified");return;}
        if(retainedPosition) {
          if(snapshot!=null&&snapshot.OccupancyKnown) {
            menuLease.Record(snapshot,retained,fingerprint,now);
            if(!menuLease.TryHold(hold,IsVisible,true,fingerprint,now,out retained)){Conceal("menu-changed");return;}
          }
          if(retained!=null)placement=retained;
        }
        if(!placement.Visible){Conceal(placement.Reason);return;}
        bool showing=!IsVisible;
        if(showing){Opacity=0;Show();}
        TaskbarArea bounds=placement.Bounds;
        IntPtr captureWindow;uint flags=0x0010;if(hold||(ContextMenu!=null&&ContextMenu.IsOpen)||(IsOtherMenuOpen!=null&&IsOtherMenuOpen())||TaskbarTransient.IsActive(out captureWindow))flags|=0x0004;
        TaskbarSnapshot orderSnapshot=retainedPosition?leaseSnapshot:snapshot;
        IntPtr? anchor=readPlacementAnchor(orderSnapshot,bounds,(flags&0x0004)!=0);
        if(!anchor.HasValue){Conceal("overlay-order-unverified");return;}
        if(!TaskbarNative.SetWindowPos(hwnd,anchor.Value,(int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height,flags)) {Conceal("position-failed");return;}
        if(!readPlacementAnchor(orderSnapshot,bounds,true).HasValue){Conceal("overlay-order-changed");return;}
        PlacementStatus=placement.Reason;Opacity=1;
        if(!hold) {
          string evidence=readLeaseFingerprint(snapshot,bounds);
          long age=source==null?0:source.SnapshotAge(measurement);
          diagnosticAge=age;diagnosticFingerprint=evidence;diagnosticProof=new {reason=TaskbarNative.LastFingerprintReason,parts=TaskbarNative.LastFingerprintParts};diagnosticBaselineProof=diagnosticProof;
          menuLease.Record(snapshot,placement,evidence,placementMilliseconds()-age);
          leaseSnapshot=snapshot;lastShown=placement;
        }
        TracePlacement(null);
      }catch {Conceal("provider-error");}
    }
    IntPtr WindowMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
      if(message==0x0021){handled=true;return new IntPtr(3);}
      if(message==0x007E||message==0x001A||message==0x02E0){TracePlacement("native-message:"+message.ToString("X")+":"+wParam.ToInt64().ToString("X"));if(source!=null)source.Invalidate();InvalidateMenuLease();Conceal("layout-changed");}
      return IntPtr.Zero;
    }
    void Stop() {Enabled=false;timer.Stop();InvalidateMenuLease();if(source!=null)source.Dispose();PlacementStatus="disposed";}
    public void Dispose() {if(disposed)return;disposed=true;Stop();if(!closed)Close();}
  }

  // This reader owns no shell windows and performs accessibility work only on one background worker.
  internal sealed class TaskbarSnapshotSource:IDisposable {
    internal sealed class Measurement {
      internal readonly TaskbarSnapshotSource Owner;
      internal readonly long MeasuredAt,ReadAt;
      internal readonly int Generation;
      internal Measurement(TaskbarSnapshotSource owner,long measuredAt,long readAt,int generation){Owner=owner;MeasuredAt=measuredAt;ReadAt=readAt;Generation=generation;}
    }
    const long LifetimeMilliseconds=1600,ScanIntervalMilliseconds=350,MaximumScanMilliseconds=1200;
    readonly object gate=new object();
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly Func<TaskbarSnapshot> scan;
    readonly Func<long> nowMilliseconds;
    readonly Func<TaskbarSnapshot,bool> safety;
    TaskbarSnapshot snapshot;
    long measuredAt=-LifetimeMilliseconds,nextScan;
    int generation;
    bool running,disposed;
    public IntPtr OwnWindow;
    public TaskbarSnapshotSource(Func<TaskbarSnapshot> scan=null,Func<long> nowMilliseconds=null,Func<TaskbarSnapshot,bool> safety=null) {
      this.scan=scan??delegate{return TaskbarNative.ReadSnapshot(OwnWindow);};
      this.nowMilliseconds=nowMilliseconds??delegate{return clock.ElapsedMilliseconds;};
      this.safety=safety??delegate(TaskbarSnapshot value){return TaskbarNative.CanDisplaySnapshot(value,OwnWindow);};
    }
    public TaskbarSnapshot Read(){Measurement ignored;return Read(out ignored);}
    public TaskbarSnapshot Read(out Measurement measurement) {
      measurement=null;
      TaskbarSnapshot candidate;
      lock(gate) {
        if(disposed)return null;
        long now=nowMilliseconds();
        if(!running&&now>=nextScan) {
          running=true;nextScan=now+ScanIntervalMilliseconds;int requestedGeneration=generation;
          ThreadPool.QueueUserWorkItem(delegate {
            TaskbarSnapshot result=null;long started=nowMilliseconds();
            try{result=scan();}catch{}
            lock(gate){running=false;if(disposed||generation!=requestedGeneration)return;if(nowMilliseconds()-started>MaximumScanMilliseconds)result=null;snapshot=result;measuredAt=started;}
          });
        }
        candidate=now>=measuredAt&&now-measuredAt<=LifetimeMilliseconds?snapshot:null;
        if(candidate!=null)measurement=new Measurement(this,measuredAt,now,generation);
      }
      // Fast native checks never traverse UIA: fullscreen/autohide cannot reuse a stale safe snapshot.
      if(candidate!=null&&!safety(candidate)){Invalidate();measurement=null;return null;}
      lock(gate){if(disposed||(measurement!=null&&measurement.Generation!=generation)){measurement=null;return null;}}
      return candidate;
    }
    // A receipt describes the measurement captured by this read, even when a
    // newer scan publishes before the renderer finishes its native checks.
    public long SnapshotAge(Measurement measurement){lock(gate){long now=nowMilliseconds();return !disposed&&measurement!=null&&Object.ReferenceEquals(measurement.Owner,this)&&measurement.Generation==generation&&measurement.MeasuredAt>=0&&now>=measurement.ReadAt&&now>=measurement.MeasuredAt?now-measurement.MeasuredAt:LifetimeMilliseconds+1;}}
    public void Invalidate(){lock(gate){generation++;snapshot=null;nextScan=0;}}
    public long SnapshotAge(TaskbarSnapshot value){lock(gate)return value!=null&&Object.ReferenceEquals(value,snapshot)?nowMilliseconds()-measuredAt:LifetimeMilliseconds+1;}
    public object SnapshotDebug(TaskbarSnapshot candidate){lock(gate)return new {running=running,disposed=disposed,generation=generation,age=nowMilliseconds()-measuredAt,measuredAt=measuredAt,currentKnown=snapshot!=null&&snapshot.OccupancyKnown,candidateIsCurrent=Object.ReferenceEquals(candidate,snapshot)};}
    public void Dispose(){lock(gate){disposed=true;generation++;snapshot=null;}}
  }

  internal static class TaskbarNative {
    [ThreadStatic] internal static string LastFingerprintReason;
    [ThreadStatic] internal static object LastFingerprintParts;
    [StructLayout(LayoutKind.Sequential)] internal struct Rect {public int Left,Top,Right,Bottom;public TaskbarArea Area(){return new TaskbarArea{X=Left,Y=Top,Width=Right-Left,Height=Bottom-Top};}}
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
    [StructLayout(LayoutKind.Sequential)] struct AppbarData {public uint Size;public IntPtr Window;public uint Callback,Edge;public Rect Bounds;public IntPtr Parameter;}
    delegate bool EnumWindowProc(IntPtr window,IntPtr parameter);
    const uint MonitorNearest=2;
    static readonly TaskbarFullscreenMemory fullscreenMemory=new TaskbarFullscreenMemory();
    static bool Intersects(TaskbarArea a,TaskbarArea b){return a.X<b.X+b.Width&&a.X+a.Width>b.X&&a.Y<b.Y+b.Height&&a.Y+a.Height>b.Y;}
    static string ClassName(IntPtr window){var text=new System.Text.StringBuilder(256);return GetClassName(window,text,text.Capacity)>0?text.ToString():"";}
    static bool BackgroundClass(string value){return value=="Progman"||value=="WorkerW"||value=="Shell_TrayWnd"||value=="Shell_SecondaryTrayWnd";}
    internal static bool ProtectNativeWindow(string className,bool explorerProcess,bool nativeLeaf) {
      if(!explorerProcess||className=="Start"||className=="TrayNotifyWnd"||className=="TrayShowDesktopButtonWClass")return true;
      // These HWNDs describe shell layout space, not necessarily painted content across their whole bounds.
      switch(className) {
        case "ReBarWindow32":case "MSTaskSwWClass":case "MSTaskListWClass":
        case "Windows.UI.Composition.DesktopWindowContentBridge":case "Windows.UI.Input.InputSite.WindowClass":
        case "InputSite.WindowClass":case "XamlExplorerHostIslandWindow":return false;
      }
      // In-process deskbands may have Explorer's PID and expose only a Pane in UIA.
      return nativeLeaf;
    }
    internal static bool IsTaskbarFullyRevealed(TaskbarArea bar,TaskbarArea monitor,double scale) {
      if(!TaskbarLayout.Valid(bar)||!TaskbarLayout.Valid(monitor)||!TaskbarLayout.Finite(scale)||scale<=0)return false;
      bool horizontal=bar.Width>bar.Height;
      if(horizontal)return bar.Height>=Math.Ceiling(TaskbarLayout.Height*scale)&&bar.Height<monitor.Height&&bar.Y>=monitor.Y&&bar.Y+bar.Height<=monitor.Y+monitor.Height+0.5&&Math.Abs(bar.X-monitor.X)<0.5&&Math.Abs(bar.Width-monitor.Width)<0.5&&(Math.Abs(bar.Y-monitor.Y)<0.5||Math.Abs(bar.Y+bar.Height-monitor.Y-monitor.Height)<0.5);
      return bar.Height>bar.Width&&bar.Width>=Math.Ceiling(TaskbarLayout.Width*scale)&&bar.Width<monitor.Width&&bar.X>=monitor.X&&bar.X+bar.Width<=monitor.X+monitor.Width+0.5&&Math.Abs(bar.Y-monitor.Y)<0.5&&Math.Abs(bar.Height-monitor.Height)<0.5&&(Math.Abs(bar.X-monitor.X)<0.5||Math.Abs(bar.X+bar.Width-monitor.X-monitor.Width)<0.5);
    }
    internal static bool IsAutoHideHidden(bool autoHideEnabled,TaskbarArea bar,TaskbarArea monitor,double scale) {return autoHideEnabled&&!IsTaskbarFullyRevealed(bar,monitor,scale);}
    internal static bool IsFullscreenBounds(TaskbarArea bounds,TaskbarArea monitor,bool maximized,long style) {
      if(!TaskbarLayout.Valid(bounds)||!TaskbarLayout.Valid(monitor))return true;
      bool framed=(style&0x00C00000L)==0x00C00000L||(style&0x00040000L)!=0;
      if(maximized&&framed)return false;
      return bounds.X<=monitor.X+1&&bounds.Y<=monitor.Y+1&&bounds.X+bounds.Width>=monitor.X+monitor.Width-1&&bounds.Y+bounds.Height>=monitor.Y+monitor.Height-1;
    }
    static bool Cloaked(IntPtr window){int value;return DwmGetWindowAttribute(window,14,out value,4)==0&&value!=0;}
    static bool WindowBounds(IntPtr window,out Rect bounds){return DwmGetWindowAttribute(window,9,out bounds,Marshal.SizeOf(typeof(Rect)))==0||GetWindowRect(window,out bounds);}
    static bool RememberFullscreen(IntPtr foreground,IntPtr monitor,Rect monitorBounds,bool fullscreen) {
      if(GetForegroundWindow()==foreground)fullscreenMemory.Confirm(monitor,monitorBounds.Left,monitorBounds.Top,monitorBounds.Right,monitorBounds.Bottom,fullscreen);
      return fullscreen;
    }
    static bool Fullscreen(IntPtr own,IntPtr monitor,Rect monitorBounds) {
      IntPtr foreground=GetForegroundWindow();
      if(!TaskbarTransient.MaySuppressForFullscreen(foreground))return fullscreenMemory.DuringTransient(monitor,monitorBounds.Left,monitorBounds.Top,monitorBounds.Right,monitorBounds.Bottom);
      if(foreground==own||BackgroundClass(ClassName(foreground))||!IsWindowVisible(foreground)||IsIconic(foreground))return RememberFullscreen(foreground,monitor,monitorBounds,false);
      if(MonitorFromWindow(foreground,MonitorNearest)!=monitor)return RememberFullscreen(foreground,monitor,monitorBounds,false);
      Rect rect;if(!WindowBounds(foreground,out rect))return RememberFullscreen(foreground,monitor,monitorBounds,true);
      return RememberFullscreen(foreground,monitor,monitorBounds,IsFullscreenBounds(rect.Area(),monitorBounds.Area(),IsZoomed(foreground),GetWindowLongPtr(foreground,-16).ToInt64()));
    }
    internal static bool CanDisplaySnapshot(TaskbarSnapshot snapshot,IntPtr own) {
      try {
        if(snapshot.Handle==IntPtr.Zero||FindWindow("Shell_TrayWnd",null)!=snapshot.Handle||!IsWindowVisible(snapshot.Handle)||Cloaked(snapshot.Handle))return false;
        Rect current;if(!GetWindowRect(snapshot.Handle,out current))return false;TaskbarArea area=current.Area();
        if(area.X!=snapshot.Bounds.X||area.Y!=snapshot.Bounds.Y||area.Width!=snapshot.Bounds.Width||area.Height!=snapshot.Bounds.Height||Math.Abs(GetDpiForWindow(snapshot.Handle)/96.0-snapshot.Scale)>0.01)return false;
        IntPtr monitor=MonitorFromWindow(snapshot.Handle,MonitorNearest);var information=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};
        return GetMonitorInfo(monitor,ref information)&&IsTaskbarFullyRevealed(area,information.Monitor.Area(),snapshot.Scale)&&!Fullscreen(own,monitor,information.Monitor);
      }catch{return false;}
    }
    internal static IntPtr? PlacementAnchor(TaskbarSnapshot snapshot,TaskbarArea footprint,IntPtr own,bool preserve,Func<IntPtr,bool> passive=null) {
      try {
        if(snapshot==null||snapshot.Handle==IntPtr.Zero||!IsWindowVisible(snapshot.Handle)||!TaskbarLayout.Valid(snapshot.Bounds)||!TaskbarLayout.Valid(footprint))return null;
        Rect current;if(!GetWindowRect(snapshot.Handle,out current))return null;TaskbarArea bar=current.Area();
        if(bar.X!=snapshot.Bounds.X||bar.Y!=snapshot.Bounds.Y||bar.Width!=snapshot.Bounds.Width||bar.Height!=snapshot.Bounds.Height)return null;
        if(passive==null)passive=TaskbarPassiveOverlay.IsPassiveCanvas;
        double margin=Math.Ceiling(TaskbarLayout.Clearance*snapshot.Scale);
        var protectedFootprint=new TaskbarArea{X=footprint.X-margin,Y=footprint.Y-margin,Width=footprint.Width+2*margin,Height=footprint.Height+2*margin};
        IntPtr anchor=new IntPtr(-1);bool found=false,safe=true;int count=0;uint ownProcess=(uint)Process.GetCurrentProcess().Id;
        EnumWindows(delegate(IntPtr window,IntPtr unused){
          if(window==snapshot.Handle){found=true;return false;}
          if(++count>1024){safe=false;return false;}
          if(window==own||!IsWindowVisible(window)||IsIconic(window)||Cloaked(window)||BackgroundClass(ClassName(window)))return true;
          uint process;GetWindowThreadProcessId(window,out process);if(process==0){safe=false;return false;}
          Rect rect;if(!WindowBounds(window,out rect)){safe=false;return false;}TaskbarArea area=rect.Area();
          if(!TaskbarLayout.Valid(area)||!Intersects(area,protectedFootprint))return true;
          if(passive(window)) {
            anchor=window;
            if(preserve&&!TaskbarPassiveOverlay.IsAbove(window,own)){safe=false;return false;}
            return true;
          }
          if(process==ownProcess||TaskbarTransient.IsCaptureWindow(window)||TaskbarTransient.IsMenuWindow(window))return true;
          safe=false;return false;
        },IntPtr.Zero);
        if(!found||!safe)return null;
        // The bar can be raised between scans. Even if it now precedes every
        // canvas, an old insertion anchor must not leave our HWND behind it.
        if(preserve&&(!IsWindowVisible(own)||!TaskbarPassiveOverlay.IsAbove(own,snapshot.Handle)))return null;
        return anchor;
      }catch{return null;}
    }
    internal static string LeaseFingerprint(TaskbarSnapshot snapshot,TaskbarArea footprint,IntPtr own) {
      return LeaseFingerprintCore(snapshot,footprint,own,CanDisplaySnapshot,TaskbarPassiveOverlay.IsPassiveCanvas,(uint)Process.GetCurrentProcess().Id);
    }
    // Testable native collector: the production entry always supplies the live
    // snapshot guard, protected passive-canvas identity and actual own PID.
    internal static string LeaseFingerprintCore(TaskbarSnapshot snapshot,TaskbarArea footprint,IntPtr own,Func<TaskbarSnapshot,IntPtr,bool> canDisplay,Func<IntPtr,bool> passive,uint ownProcess) {
      LastFingerprintReason="running";LastFingerprintParts=null;
      var shellRows=new List<string>();var childRows=new List<string>();var appRows=new List<string>();
      int childCount=0,appCount=0;object blocker=null;
      Func<List<string>,string> componentHash=delegate(List<string> values) {
        string[] sorted=values.ToArray();Array.Sort(sorted,StringComparer.Ordinal);
        using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(String.Join("\n",sorted))));
      };
      Func<string,string> finish=delegate(string reason) {
        LastFingerprintReason=reason;
        LastFingerprintParts=new {complete=reason=="ok",shell=componentHash(shellRows),children=componentHash(childRows),apps=componentHash(appRows),shellRows=shellRows.Count,childRows=childRows.Count,appRows=appRows.Count,childEnumerated=childCount,appEnumerated=appCount,blocker=blocker};
        return null;
      };
      try {
        if(snapshot==null)return finish("snapshot-missing");
        if(!TaskbarLayout.Valid(footprint))return finish("footprint-invalid");
        if(!canDisplay(snapshot,own))return finish("initial-native-unsafe");
        uint shell;GetWindowThreadProcessId(snapshot.Handle,out shell);if(shell==0)return finish("shell-process-unavailable");
        bool safe=true,above=true;int count=0;
        string unsafeReason=null;
        double margin=Math.Ceiling(TaskbarLayout.Clearance*snapshot.Scale);
        var protectedFootprint=new TaskbarArea{X=footprint.X-margin,Y=footprint.Y-margin,Width=footprint.Width+2*margin,Height=footprint.Height+2*margin};
        var rows=new List<string>();string shellRow="shell|"+snapshot.Handle.ToInt64()+"|"+shell+"|"+snapshot.Scale.ToString("R",CultureInfo.InvariantCulture);
        string barRow=LeaseArea("bar",snapshot.Bounds);rows.Add(shellRow);rows.Add(barRow);shellRows.Add(shellRow);shellRows.Add(barRow);
        EnumChildWindows(snapshot.Handle,delegate(IntPtr child,IntPtr unused) {
          childCount=++count;
          if(count>512){safe=false;unsafeReason="child-window-limit";return false;}if(!IsWindowVisible(child))return true;
          uint process;GetWindowThreadProcessId(child,out process);Rect rect;
          if(process==0){safe=false;unsafeReason="child-process-unavailable";blocker=new {handle=child.ToInt64(),@class=ClassName(child)};return false;}
          if(!GetWindowRect(child,out rect)){safe=false;unsafeReason="child-bounds-unavailable";blocker=new {handle=child.ToInt64(),process=process,@class=ClassName(child)};return false;}
          TaskbarArea area=rect.Area();if(!TaskbarLayout.Valid(area)||!Intersects(area,snapshot.Bounds))return true;
          string name=ClassName(child),row=LeaseArea("child|"+child.ToInt64()+"|"+process+"|"+name,area);rows.Add(row);childRows.Add(row);
          if(ProtectNativeWindow(name,process==shell,GetWindow(child,5)==IntPtr.Zero)&&Intersects(area,protectedFootprint)){safe=false;unsafeReason="protected-child-overlap";blocker=new {handle=child.ToInt64(),process=process,@class=name,rect=area};return false;}
          return true;
        },IntPtr.Zero);
        if(!safe)return finish(unsafeReason??"child-enumeration-unsafe");count=0;
        EnumWindows(delegate(IntPtr window,IntPtr unused) {
          appCount=++count;
          if(count>1024){safe=false;unsafeReason="app-window-limit";return false;}
          if(window==snapshot.Handle){above=false;return true;}
          if(window==own||!IsWindowVisible(window)||Cloaked(window)||BackgroundClass(ClassName(window)))return true;
          uint process;GetWindowThreadProcessId(window,out process);if(process==ownProcess)return true;
          if(TaskbarTransient.IsCaptureWindow(window)||TaskbarTransient.IsMenuWindow(window))return true;
          Rect rect;
          if(process==0){safe=false;unsafeReason="app-process-unavailable";blocker=new {handle=window.ToInt64(),@class=ClassName(window)};return false;}
          if(!WindowBounds(window,out rect)){safe=false;unsafeReason="app-bounds-unavailable";blocker=new {handle=window.ToInt64(),process=process,@class=ClassName(window)};return false;}
          TaskbarArea area=rect.Area();bool iconic=IsIconic(window);
          if(above&&!iconic&&TaskbarLayout.Valid(area)&&Intersects(area,protectedFootprint)){
            if(passive(window)&&TaskbarPassiveOverlay.IsAbove(window,own)&&TaskbarPassiveOverlay.IsAbove(own,snapshot.Handle)){
              // This layer is already outside occupied controls under the live
              // canvas > widget > taskbar proof. Its arrival or removal must not
              // invalidate a menu lease whose protected controls are unchanged.
              return true;
            }
            safe=false;unsafeReason="above-taskbar-overlap";blocker=new {handle=window.ToInt64(),process=process,@class=ClassName(window),rect=area};return false;
          }
          long extended=GetWindowLongPtr(window,-20).ToInt64();
          bool button=(extended&0x00040000L)!=0||((extended&0x80L)==0&&GetWindow(window,4)==IntPtr.Zero);
          if(button) {
            var title=new System.Text.StringBuilder(512);GetWindowText(window,title,title.Capacity);string text=title.ToString();
            string row=LeaseArea("app|"+window.ToInt64()+"|"+process+"|"+iconic+"|"+(extended&0x00040080L)+"|"+text.Length+":"+text,area);rows.Add(row);appRows.Add(row);
          }
          return true;
        },IntPtr.Zero);
        uint current;GetWindowThreadProcessId(snapshot.Handle,out current);
        if(!safe)return finish(unsafeReason??"app-enumeration-unsafe");
        if(above)return finish("taskbar-z-order-missing");
        if(current!=shell)return finish("shell-process-changed");
        if(!canDisplay(snapshot,own))return finish("final-native-unsafe");
        rows.Sort(StringComparer.Ordinal);
        finish("ok");
        using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(String.Join("\n",rows.ToArray()))));
      }catch(Exception error){LastFingerprintReason="exception/"+error.GetType().Name;return null;}
    }
    static string LeaseArea(string key,TaskbarArea area) {return key+"|"+area.X.ToString("R",CultureInfo.InvariantCulture)+"|"+area.Y.ToString("R",CultureInfo.InvariantCulture)+"|"+area.Width.ToString("R",CultureInfo.InvariantCulture)+"|"+area.Height.ToString("R",CultureInfo.InvariantCulture);}
    internal static TaskbarSnapshot ReadSnapshot(IntPtr own) {
      var result=new TaskbarSnapshot();IntPtr taskbar=FindWindow("Shell_TrayWnd",null);result.Handle=taskbar;
      Rect rectangle;if(taskbar==IntPtr.Zero||!GetWindowRect(taskbar,out rectangle)||!IsWindowVisible(taskbar)||Cloaked(taskbar))return result;
      result.Bounds=rectangle.Area();uint dpi=GetDpiForWindow(taskbar);if(dpi==0)return result;result.Scale=dpi/96.0;
      IntPtr monitor=MonitorFromWindow(taskbar,MonitorNearest);var monitorInfo=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(!GetMonitorInfo(monitor,ref monitorInfo))return result;
      // Auto-hide enablement is not hidden state: a fully revealed bar can host the label safely.
      var appbar=new AppbarData{Size=(uint)Marshal.SizeOf(typeof(AppbarData))};bool autoHideEnabled=(SHAppBarMessage(4,ref appbar).ToUInt64()&1)!=0;
      result.AutoHidden=IsAutoHideHidden(autoHideEnabled,result.Bounds,monitorInfo.Monitor.Area(),result.Scale);
      // Only a complete revealed primary edge is supported; sliding/offscreen bars hide.
      if(!IsTaskbarFullyRevealed(result.Bounds,monitorInfo.Monitor.Area(),result.Scale))return result;
      result.Visible=true;
      result.Fullscreen=Fullscreen(own,monitor,monitorInfo.Monitor);if(result.Fullscreen)return result;
      uint explorer;GetWindowThreadProcessId(taskbar,out explorer);if(explorer==0)return result;
      bool consistent=true,startFound=false,trayFound=false;
      var automationRoots=new List<IntPtr>();
      uint ownProcess=(uint)Process.GetCurrentProcess().Id;
      bool taskbarFound=ForEachWindowAbove(taskbar,delegate(IntPtr window) {
        if(window==own||!IsWindowVisible(window)||IsIconic(window)||Cloaked(window)||BackgroundClass(ClassName(window))||TaskbarTransient.IsCaptureWindow(window)||TaskbarTransient.IsMenuWindow(window))return;
        uint process;GetWindowThreadProcessId(window,out process);if(process==ownProcess)return;
        Rect bounds;if(!WindowBounds(window,out bounds)){consistent=false;return;}
        // A trusted passive canvas may span the entire display. Its pixels are
        // still protected by PlacementAnchor: our HWND must stay BELOW every
        // overlapping canvas before opacity is restored, including menu holds.
        TaskbarArea area=bounds.Area();if(TaskbarLayout.Valid(area)&&Intersects(area,result.Bounds)&&!TaskbarPassiveOverlay.IsPassiveCanvas(window))result.Occupied.Add(area);
      });
      EnumChildWindows(taskbar,delegate(IntPtr child,IntPtr unused) {
        if(!IsWindowVisible(child))return true;
        uint process;GetWindowThreadProcessId(child,out process);string name=ClassName(child);
        Rect bounds;if(!GetWindowRect(child,out bounds)){consistent=false;return true;}
        TaskbarArea area=bounds.Area();if(!TaskbarLayout.Valid(area)||!Intersects(area,result.Bounds))return true;
        if(name=="Start")startFound=true;
        if(name=="TrayNotifyWnd")trayFound=true;
        if(process==explorer&&(name=="Windows.UI.Composition.DesktopWindowContentBridge"||name=="ReBarWindow32"||name=="MSTaskListWClass"))automationRoots.Add(child);
        if(ProtectNativeWindow(name,process==explorer,GetWindow(child,5)==IntPtr.Zero))result.Occupied.Add(area);
        return true;
      },IntPtr.Zero);
      IList<AutomationElement> elements=ReadTaskbarAutomationElements(taskbar,automationRoots,result.Occupied);if(elements==null)return result;
      if(elements.Count==0||elements.Count>4096)return result;
      for(int index=0;index<elements.Count;index++) {
        AutomationElement element=elements[index];var cached=element.Cached;
        // Explorer popup activation can mark real taskbar controls offscreen.
        // Their fresh valid rectangles still reserve space; only empty hidden
        // elements may be ignored, never visible buttons with usable bounds.
        string id=cached.AutomationId;ControlType type=cached.ControlType;
        bool structural=type==ControlType.Pane||type==ControlType.Group||type==ControlType.ToolBar||type==ControlType.Window||type==ControlType.List;
        System.Windows.Rect rect=cached.BoundingRectangle;
        if(rect.IsEmpty||rect.Width<=0||rect.Height<=0){if(!structural&&!cached.IsOffscreen)consistent=false;continue;}
        var area=new TaskbarArea{X=rect.X,Y=rect.Y,Width=rect.Width,Height=rect.Height};if(!TaskbarLayout.Valid(area)){consistent=false;continue;}if(!Intersects(area,result.Bounds))continue;
        if(id=="StartButton")startFound=true;
        if(id=="SystemTrayIcon"||id=="NotifyItemIcon"||id=="SystemTrayFrame"||id=="NotificationCenterButton"||id=="ClockButton")trayFound=true;
        if(!structural||cached.ProcessId!=(int)explorer)result.Occupied.Add(area);
      }
      uint currentExplorer;GetWindowThreadProcessId(taskbar,out currentExplorer);
      result.OccupancyKnown=consistent&&taskbarFound&&startFound&&trayFound&&currentExplorer==explorer&&FindWindow("Shell_TrayWnd",null)==taskbar;
      return result;
    }
    internal static IList<AutomationElement> ReadTaskbarAutomationElements(IntPtr taskbar,IList<IntPtr> children,IList<TaskbarArea> occupied,Func<IntPtr,AutomationElementCollection> read=null) {
      if(read==null)read=ReadAutomationElements;
      var elements=new List<AutomationElement>();
      // Modal shell popups can replace the wrapper's accessibility fragment.
      // Query the concrete content HWNDs directly whenever they are available.
      var roots=new List<IntPtr>();foreach(IntPtr child in children)if(!roots.Contains(child))roots.Add(child);
      if(roots.Count==0)roots.Add(taskbar);
      foreach(IntPtr root in roots) {
        AutomationElementCollection part=null;try{part=read(root);}catch{}
        if(part==null||part.Count==0) {
          // If a concrete native subtree is unreadable, protect its entire current
          // rectangle rather than inventing free space or renewing an old snapshot.
          if(root!=taskbar){Rect bounds;if(!GetWindowRect(root,out bounds))return null;occupied.Add(bounds.Area());}
          continue;
        }
        if(part.Count>4096-elements.Count)return null;
        for(int i=0;i<part.Count;i++)elements.Add(part[i]);
      }
      return elements;
    }
    internal static bool ForEachWindowAbove(IntPtr boundary,Action<IntPtr> visit) {
      bool found=false;
      // EnumWindows walks top-level windows in Z order. Anything below the taskbar cannot obscure it.
      EnumWindows(delegate(IntPtr window,IntPtr unused){if(window==boundary){found=true;return false;}visit(window);return true;},IntPtr.Zero);
      return found;
    }
    internal static AutomationElementCollection ReadAutomationElements(IntPtr window) {
      // The root must retain a live provider handle; only returned descendants are cache-only objects.
      var root=AutomationElement.FromHandle(window);if(root==null)return null;
      var request=new CacheRequest();request.TreeFilter=Automation.RawViewCondition;request.AutomationElementMode=AutomationElementMode.None;
      foreach(AutomationProperty property in new[]{AutomationElement.BoundingRectangleProperty,AutomationElement.IsOffscreenProperty,AutomationElement.AutomationIdProperty,AutomationElement.ControlTypeProperty,AutomationElement.ProcessIdProperty})request.Add(property);
      using(request.Activate())return root.FindAll(TreeScope.Descendants,System.Windows.Automation.Condition.TrueCondition);
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string className,string name);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect rectangle);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo information);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent,EnumWindowProc callback,IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowProc callback,IntPtr parameter);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,System.Text.StringBuilder name,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,System.Text.StringBuilder text,int count);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint message,ref AppbarData data);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,uint attribute,out Rect value,int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,uint attribute,out int value,int size);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
  }
}
