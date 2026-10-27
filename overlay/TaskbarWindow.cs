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
    readonly TextBlock upper,lower;
    readonly Border surface;
    ThemePalette palette=ThemePalette.Dark;
    string side="right",upperTone="unknown",lowerTone="unknown";
    bool stale,disposed,closed;
    IntPtr hwnd;
    public bool Enabled {get;private set;}
    public string PlacementStatus {get;private set;}
    public Func<bool> IsOtherMenuOpen {get;set;}

    public TaskbarWindow(Func<TaskbarSnapshot> readSnapshot=null) {
      if(readSnapshot==null){source=new TaskbarSnapshotSource();this.readSnapshot=source.Read;}else this.readSnapshot=readSnapshot;
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
    public void Configure(bool enabled,string side,ThemePalette palette) {
      if(disposed||closed)return;
      this.side=side;this.palette=palette??ThemePalette.Dark;ApplyPalette();Enabled=enabled;
      if(!enabled){timer.Stop();if(source!=null)source.Invalidate();Conceal("disabled");return;}
      timer.Start();RefreshPlacement();
    }
    public void ApplyRows(string upper,string lower,string upperTone,string lowerTone,bool stale) {
      if(disposed||closed)return;
      this.upper.Text=Percentage(upper);this.lower.Text=Percentage(lower);this.upperTone=upperTone;this.lowerTone=lowerTone;this.stale=stale;ApplyPalette();
    }
    void ApplyPalette() {surface.Background=Color(palette.Surface);upper.Foreground=Color(palette.Tone(upperTone));lower.Foreground=Color(palette.Tone(lowerTone));upper.Opacity=lower.Opacity=stale?0.55:1;}
    void Conceal(string reason) {PlacementStatus=reason;if(IsVisible)Hide();}
    void RefreshPlacement() {
      if(disposed||closed||!Enabled)return;
      try {
        TaskbarPlacement placement=TaskbarLayout.Place(readSnapshot(),side);
        if(!placement.Visible){Conceal(placement.Reason);return;}
        bool showing=!IsVisible;
        if(showing){Opacity=0;Show();}
        TaskbarArea bounds=placement.Bounds;
        uint flags=0x0010;if((ContextMenu!=null&&ContextMenu.IsOpen)||(IsOtherMenuOpen!=null&&IsOtherMenuOpen()))flags|=0x0004;
        if(!TaskbarNative.SetWindowPos(hwnd,new IntPtr(-1),(int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height,flags)) {Conceal("position-failed");return;}
        PlacementStatus="visible";Opacity=1;
      }catch {Conceal("provider-error");}
    }
    IntPtr WindowMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
      if(message==0x0021){handled=true;return new IntPtr(3);}
      if(message==0x007E||message==0x001A||message==0x02E0){if(source!=null)source.Invalidate();Conceal("layout-changed");}
      return IntPtr.Zero;
    }
    void Stop() {Enabled=false;timer.Stop();if(source!=null)source.Dispose();PlacementStatus="disposed";}
    public void Dispose() {if(disposed)return;disposed=true;Stop();if(!closed)Close();}
  }

  // This reader owns no shell windows and performs accessibility work only on one background worker.
  internal sealed class TaskbarSnapshotSource:IDisposable {
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
    public TaskbarSnapshot Read() {
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
        candidate=now-measuredAt<=LifetimeMilliseconds?snapshot:null;
      }
      // Fast native checks never traverse UIA: fullscreen/autohide cannot reuse a stale safe snapshot.
      if(candidate!=null&&!safety(candidate)){Invalidate();return null;}
      return candidate;
    }
    public void Invalidate(){lock(gate){generation++;snapshot=null;nextScan=0;}}
    public void Dispose(){lock(gate){disposed=true;generation++;snapshot=null;}}
  }

  internal static class TaskbarNative {
    [StructLayout(LayoutKind.Sequential)] internal struct Rect {public int Left,Top,Right,Bottom;public TaskbarArea Area(){return new TaskbarArea{X=Left,Y=Top,Width=Right-Left,Height=Bottom-Top};}}
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
    [StructLayout(LayoutKind.Sequential)] struct AppbarData {public uint Size;public IntPtr Window;public uint Callback,Edge;public Rect Bounds;public IntPtr Parameter;}
    delegate bool EnumWindowProc(IntPtr window,IntPtr parameter);
    const uint MonitorNearest=2;
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
      return bar.Width>bar.Height&&bar.Height>=Math.Ceiling(TaskbarLayout.Height*scale)&&bar.Y>=monitor.Y&&Math.Abs(bar.Y+bar.Height-monitor.Y-monitor.Height)<0.5&&Math.Abs(bar.X-monitor.X)<0.5&&Math.Abs(bar.X+bar.Width-monitor.X-monitor.Width)<0.5;
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
    static bool Fullscreen(IntPtr own,IntPtr monitor,Rect monitorBounds) {
      IntPtr foreground=GetForegroundWindow();if(foreground==IntPtr.Zero)return true;
      if(foreground==own||BackgroundClass(ClassName(foreground))||!IsWindowVisible(foreground)||IsIconic(foreground))return false;
      if(MonitorFromWindow(foreground,MonitorNearest)!=monitor)return false;
      Rect rect;if(!WindowBounds(foreground,out rect))return true;
      return IsFullscreenBounds(rect.Area(),monitorBounds.Area(),IsZoomed(foreground),GetWindowLongPtr(foreground,-16).ToInt64());
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
    internal static TaskbarSnapshot ReadSnapshot(IntPtr own) {
      var result=new TaskbarSnapshot();IntPtr taskbar=FindWindow("Shell_TrayWnd",null);result.Handle=taskbar;
      Rect rectangle;if(taskbar==IntPtr.Zero||!GetWindowRect(taskbar,out rectangle)||!IsWindowVisible(taskbar)||Cloaked(taskbar))return result;
      result.Bounds=rectangle.Area();uint dpi=GetDpiForWindow(taskbar);if(dpi==0)return result;result.Scale=dpi/96.0;
      IntPtr monitor=MonitorFromWindow(taskbar,MonitorNearest);var monitorInfo=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(!GetMonitorInfo(monitor,ref monitorInfo))return result;
      // Auto-hide enablement is not hidden state: a fully revealed bar can host the label safely.
      var appbar=new AppbarData{Size=(uint)Marshal.SizeOf(typeof(AppbarData))};bool autoHideEnabled=(SHAppBarMessage(4,ref appbar).ToUInt64()&1)!=0;
      result.AutoHidden=IsAutoHideHidden(autoHideEnabled,result.Bounds,monitorInfo.Monitor.Area(),result.Scale);
      // Only a complete readable horizontal primary bottom row is supported; sliding/offscreen rows hide.
      if(!IsTaskbarFullyRevealed(result.Bounds,monitorInfo.Monitor.Area(),result.Scale))return result;
      result.Visible=true;
      result.Fullscreen=Fullscreen(own,monitor,monitorInfo.Monitor);if(result.Fullscreen)return result;
      uint explorer;GetWindowThreadProcessId(taskbar,out explorer);if(explorer==0)return result;
      bool consistent=true,startFound=false,trayFound=false;
      EnumChildWindows(taskbar,delegate(IntPtr child,IntPtr unused) {
        if(!IsWindowVisible(child))return true;
        uint process;GetWindowThreadProcessId(child,out process);string name=ClassName(child);
        Rect bounds;if(!GetWindowRect(child,out bounds)){consistent=false;return true;}
        TaskbarArea area=bounds.Area();if(!TaskbarLayout.Valid(area)||!Intersects(area,result.Bounds))return true;
        if(name=="Start")startFound=true;
        if(name=="TrayNotifyWnd")trayFound=true;
        if(ProtectNativeWindow(name,process==explorer,GetWindow(child,5)==IntPtr.Zero))result.Occupied.Add(area);
        return true;
      },IntPtr.Zero);
      AutomationElementCollection elements=ReadAutomationElements(taskbar);if(elements==null)return result;
      if(elements.Count==0||elements.Count>4096)return result;
      for(int index=0;index<elements.Count;index++) {
        AutomationElement element=elements[index];var cached=element.Cached;if(cached.IsOffscreen)continue;
        string id=cached.AutomationId;ControlType type=cached.ControlType;
        bool structural=type==ControlType.Pane||type==ControlType.Group||type==ControlType.ToolBar||type==ControlType.Window||type==ControlType.List;
        System.Windows.Rect rect=cached.BoundingRectangle;
        if(rect.IsEmpty||rect.Width<=0||rect.Height<=0){if(!structural)consistent=false;continue;}
        var area=new TaskbarArea{X=rect.X,Y=rect.Y,Width=rect.Width,Height=rect.Height};if(!TaskbarLayout.Valid(area)){consistent=false;continue;}if(!Intersects(area,result.Bounds))continue;
        if(id=="StartButton")startFound=true;
        if(id=="SystemTrayIcon"||id=="NotifyItemIcon"||id=="SystemTrayFrame"||id=="NotificationCenterButton"||id=="ClockButton")trayFound=true;
        if(!structural||cached.ProcessId!=(int)explorer)result.Occupied.Add(area);
      }
      uint ownProcess=(uint)Process.GetCurrentProcess().Id;
      bool taskbarFound=ForEachWindowAbove(taskbar,delegate(IntPtr window) {
        if(window==own||!IsWindowVisible(window)||IsIconic(window)||Cloaked(window)||BackgroundClass(ClassName(window)))return;
        uint process;GetWindowThreadProcessId(window,out process);if(process==ownProcess)return;
        Rect bounds;if(!WindowBounds(window,out bounds)){consistent=false;return;}
        TaskbarArea area=bounds.Area();if(TaskbarLayout.Valid(area)&&Intersects(area,result.Bounds))result.Occupied.Add(area);
      });
      uint currentExplorer;GetWindowThreadProcessId(taskbar,out currentExplorer);
      result.OccupancyKnown=consistent&&taskbarFound&&startFound&&trayFound&&currentExplorer==explorer&&FindWindow("Shell_TrayWnd",null)==taskbar;
      return result;
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
