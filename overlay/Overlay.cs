using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace QuotaOverlay {
  internal static class Native {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
    public delegate bool EnumProc(IntPtr hwnd,IntPtr param);
    public delegate void EventProc(IntPtr hook,uint ev,IntPtr hwnd,int obj,int child,uint thread,uint time);
    public delegate IntPtr MouseProc(int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SetWindowsHookEx(int id,MouseProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb,IntPtr p);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd,uint command);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd,out Rect r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd,ref Point p);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd,StringBuilder b,int n);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int w,int h,uint flags);
    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr dll,EventProc cb,uint pid,uint tid,uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd,int attr,out int value,int size);
  }

  // Exists only while our context menu is open. Observe button-down without
  // capturing/suppressing input; clicks on our own popup HWNDs stay inside.
  internal sealed class MenuDismissWatcher : IDisposable {
    readonly Dispatcher dispatcher;
    readonly Func<bool> isOpen;
    readonly Action dismiss;
    readonly Func<Native.Point,uint> processAtPoint;
    readonly uint ownProcess=(uint)Process.GetCurrentProcess().Id;
    readonly Native.MouseProc callback;
    int session;
    bool disposed;
    public IntPtr Hook {get;private set;}
    public MenuDismissWatcher(Dispatcher owner,Func<bool> opened,Action close,Func<Native.Point,uint> lookup) {
      dispatcher=owner;isOpen=opened;dismiss=close;processAtPoint=lookup??LookupProcess;callback=Receive;
    }
    static uint LookupProcess(Native.Point point) {
      uint process;Native.GetWindowThreadProcessId(Native.WindowFromPoint(point),out process);return process;
    }
    public void Start() {
      if(disposed||Hook!=IntPtr.Zero)return;
      session++;Hook=Native.SetWindowsHookEx(14,callback,Native.GetModuleHandle(null),0);
    }
    public void Stop() {
      session++;if(Hook!=IntPtr.Zero){Native.UnhookWindowsHookEx(Hook);Hook=IntPtr.Zero;}
    }
    IntPtr Receive(int code,IntPtr message,IntPtr data) {
      try {Observe(code,message,data);}catch{}
      // Always forward the original click, including clicks that dismiss us.
      return Native.CallNextHookEx(Hook,code,message,data);
    }
    internal void Observe(int code,IntPtr message,IntPtr data) {
      int kind=message.ToInt32();
      if(code!=0||Hook==IntPtr.Zero||!isOpen()||(kind!=0x201&&kind!=0x204&&kind!=0x207&&kind!=0x20B))return;
      Native.Point point=(Native.Point)Marshal.PtrToStructure(data,typeof(Native.Point));int capturedSession=session;
      dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(delegate {
        if(disposed||capturedSession!=session||Hook==IntPtr.Zero||!isOpen())return;
        if(processAtPoint(point)!=ownProcess)dismiss();
      }));
    }
    public void Dispose() {Stop();disposed=true;}
  }

  public class OverlayWindow : Window {
    readonly JavaScriptSerializer json=new JavaScriptSerializer();
    readonly TextBlock[] periods=new TextBlock[2],values=new TextBlock[2],dates=new TextBlock[2],times=new TextBlock[2],markers=new TextBlock[2];
    readonly string stateFile,settingsFile;
    readonly DispatcherTimer tracker=new DispatcherTimer();
    readonly DispatcherTimer motionTracker=new DispatcherTimer(DispatcherPriority.Render);
    readonly Forms.NotifyIcon tray=new Forms.NotifyIcon();
    readonly Forms.ToolStripMenuItem pause=new Forms.ToolStripMenuItem();
    Native.EventProc nativeEvent;
    IntPtr eventHook,locationHook,moveHook,minimizeHook,hwnd,target;
    bool placing,refreshQueued,pendingPreview,pendingStartupCheckpoint,interactiveMove;
    long motionDeadline;
    EventWaitHandle stopEvent;
    RegisteredWaitHandle stopWait;
    bool paused,closing,started,preferCompact,compact;
    string preferredMode,presentationMode;
    string themeMode,effectiveTheme;
    string language;
    ThemePalette palette;
    readonly ThemeResolver themeResolver;
    Func<IntPtr,bool> targetMatcher;
    readonly string[] rowTones=new[]{"unknown","unknown"};
    DateTime nextThemeCheck=DateTime.MinValue;
    string lastState="",previewError="",stateWriteError="";
    int receivedUpdates;
    object view,diagnosticState;
    Border panel,divider;
    Grid surface;
    readonly Border[] progressTracks=new Border[2],progressFills=new Border[2];
    readonly double?[] remainingQuota=new double?[2];
    System.Windows.Shapes.Path progressOutline;
    ContextMenu optionsMenu;
    MenuDismissWatcher menuDismiss;
    MenuItem fullProgressItem,shortProgressItem,weeklyProgressItem,compactModeItem,pauseUpdatesItem;
    MenuItem fullOutlineItem,fullBothItem,fullOffItem,contourSourceItem;
    MenuItem fullModeItem,ultraModeItem;
    MenuItem themeDarkItem,themeLightItem,themeAutoItem;
    MenuItem languageRussianItem,languageEnglishItem;
    MenuItem autostartItem;
    MenuItem taskbarEnabledItem,taskbarLeftItem,taskbarRightItem,taskbarStatusItem;
    Forms.ToolStripMenuItem taskbarTrayItem,taskbarTraySides,taskbarTrayLeft,taskbarTrayRight;
    TaskbarWindow taskbarWindow;
    ContextMenu taskbarMenu;
    MenuDismissWatcher taskbarMenuDismiss;
    bool taskbarEnabled;
    string taskbarSide;
    int backgroundTransparency;
    Slider transparencySlider;
    TextBlock transparencyLabel;
    bool syncingTransparency;
    readonly Func<TaskbarSnapshot> taskbarSnapshotReader;
    MenuItem updatesItem;
    Window updateDialog;
    Action<Window> showUpdateDialog=delegate(Window dialog){dialog.Show();};
    Action<string> openReleasePage=delegate(string url){Process.Start(new ProcessStartInfo(url){UseShellExecute=true});};
    bool autoStartEnabled,autostartBusy,appSignalSent,lastAppReady;
    readonly AppStartupGate appStartup=new AppStartupGate();
    bool showFullProgress,showCompactProgress;
    bool showUltraProgress;
    string fullProgressMode,contourWindow;
    TextBlock compactStale;

    public OverlayWindow(string statePath):this(statePath,null) { }
    internal OverlayWindow(string statePath,ThemeResolver resolver,Func<IntPtr,bool> matchTarget=null,Func<TaskbarSnapshot> readTaskbarSnapshot=null) {
      targetMatcher=matchTarget;
      taskbarSnapshotReader=readTaskbarSnapshot;
      stateFile=statePath;
      StartupCheckpoint("constructing");
      settingsFile=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(statePath)),"settings.json");
      var preferences=ReadPreferences();
      taskbarEnabled=BooleanPreference(preferences,"taskbarEnabled",false);
      taskbarSide=preferences.ContainsKey("taskbarSide")&&Convert.ToString(preferences["taskbarSide"])=="right"?"right":"left";
      object savedTransparency;
      backgroundTransparency=preferences.TryGetValue("backgroundTransparency",out savedTransparency)&&savedTransparency is int&&(int)savedTransparency>=0&&(int)savedTransparency<=100?(int)savedTransparency:50;
      language=PanelLanguage.Read(settingsFile);
      autoStartEnabled=BooleanPreference(preferences,"autoStartEnabled",true);
      string savedTheme=preferences.ContainsKey("themeMode")?Convert.ToString(preferences["themeMode"]):"";
      themeMode=savedTheme=="dark"||savedTheme=="light"||savedTheme=="auto"?savedTheme:"auto";
      themeResolver=resolver??new ThemeResolver(ThemeResolver.DefaultConfigPath(),null);
      effectiveTheme=themeResolver.Resolve(themeMode);palette=effectiveTheme=="light"?ThemePalette.Light:ThemePalette.Dark;
      string savedMode=preferences.ContainsKey("displayMode")?Convert.ToString(preferences["displayMode"]):"";
      preferredMode=savedMode=="full"||savedMode=="compact"||savedMode=="ultra"?savedMode:BooleanPreference(preferences,"compact",true)?"compact":"full";
      preferCompact=preferredMode!="full";
      showFullProgress=BooleanPreference(preferences,"showFullProgress",true);
      string savedFullMode=preferences.ContainsKey("fullProgressMode")?Convert.ToString(preferences["fullProgressMode"]):"";
      if(preferences.ContainsKey("indicatorMode")) {
        string sharedMode=Convert.ToString(preferences["indicatorMode"]);
        if(sharedMode=="bars"||sharedMode=="outline"||sharedMode=="both"||sharedMode=="off")savedFullMode=sharedMode;
      }
      fullProgressMode=savedFullMode=="bars"||savedFullMode=="outline"||savedFullMode=="both"||savedFullMode=="off"?savedFullMode:showFullProgress?"bars":"off";
      showFullProgress=fullProgressMode!="off";
      showCompactProgress=showUltraProgress=fullProgressMode=="outline"||fullProgressMode=="both";
      string legacySource=preferredMode=="full"?"fullProgressWindow":preferredMode=="ultra"?"ultraProgressWindow":"compactProgressWindow";
      string savedSource=preferences.ContainsKey("contourWindow")?Convert.ToString(preferences["contourWindow"]):"";
      contourWindow=savedSource=="short"||savedSource=="weekly"?savedSource:preferences.ContainsKey(legacySource)&&Convert.ToString(preferences[legacySource])=="weekly"?"weekly":"short";
      Title="Quota Panel";
      Width=Layout.PanelWidth; Height=Layout.PanelHeight;
      WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
      AllowsTransparency=true; Background=TaskbarAppearance.InputSurface(backgroundTransparency);
      ShowInTaskbar=false; ShowActivated=false; Topmost=false; Focusable=false;
      FontFamily=new FontFamily("Segoe UI");
      panel=new Border { Width=Layout.PanelWidth,Height=Layout.PanelHeight,Background=Brush(palette.Background(backgroundTransparency)),BorderBrush=Brush(palette.Border),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=new Thickness(3,5,3,5) };
      ToolTipService.SetIsEnabled(panel,false);
      var stack=new StackPanel(); panel.Child=stack;
      for(int i=0;i<2;i++) {
        if(i==1) {divider=new Border { Height=1,Background=Brush(palette.Divider),Margin=new Thickness(1,5,1,5) };stack.Children.Add(divider);}
        periods[i]=Text(T(i==0?"shortLabel":"weekLabel"),10,FontWeights.Normal,palette.Secondary);
        values[i]=Text("—",16,FontWeights.SemiBold,palette.Primary);
        dates[i]=Text("—",10.5,FontWeights.Normal,palette.Date);
        times[i]=Text("—",11.5,FontWeights.Bold,palette.Primary);
        markers[i]=Text("",8,FontWeights.Normal,palette.Secondary);
        foreach(var t in new[]{periods[i],values[i],dates[i],times[i]}) stack.Children.Add(t);
        progressFills[i]=new Border {Height=3,Width=0,CornerRadius=new CornerRadius(1.5),HorizontalAlignment=HorizontalAlignment.Left};
        progressTracks[i]=new Border {Width=32,Height=3,CornerRadius=new CornerRadius(1.5),Background=Brush(palette.Track),Margin=new Thickness(3,3,3,1),Child=progressFills[i]};
        stack.Children.Add(progressTracks[i]);stack.Children.Add(markers[i]);
      }
      compactStale=Text("",8,FontWeights.Normal,palette.Secondary);stack.Children.Add(compactStale);
      panel.Cursor=Cursors.Hand;
      panel.MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e) {ToggleMode();e.Handled=true;};
      surface=new Grid {Width=Layout.PanelWidth,Height=Layout.PanelHeight};surface.Children.Add(panel);
      progressOutline=new System.Windows.Shapes.Path {Width=Layout.PanelWidth,Height=Layout.CompactHeight,StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,IsHitTestVisible=false,Data=Geometry.Empty,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
      surface.Children.Add(progressOutline);
      SetupPanelMenu();
      Content=new QuotaVisualHost {Child=surface};
      SyncTaskbar();
      StartupCheckpoint("rendering-initial");SetMode(preferredMode);StartupCheckpoint("initial-rendered");
      SourceInitialized+=delegate {
        hwnd=new WindowInteropHelper(this).Handle;
        StartupCheckpoint("source-initialized");
        long style=Native.GetWindowLongPtr(hwnd,-20).ToInt64();
        Native.SetWindowLongPtr(hwnd,-20,new IntPtr((style|0x08000000L|0x00000080L)&~0x00000020L));
        HwndSource.FromHwnd(hwnd).AddHook(WindowProc);
      };
      Loaded+=delegate { if(started)return;started=true;StartupCheckpoint("loaded");Hide(); SetupTray(); StartInput(); UpdatePlacement();StartupCheckpoint("ready"); };
      Closed+=delegate {
        closing=true;if(optionsMenu!=null)optionsMenu.IsOpen=false;
        if(updateDialog!=null)updateDialog.Close();
        DisposeTaskbarView();
        if(menuDismiss!=null)menuDismiss.Dispose();StopTracking();
        if(stopWait!=null)stopWait.Unregister(null);if(stopEvent!=null)stopEvent.Dispose();
        tray.Visible=false;tray.Dispose();Command("exit");
      };
      stopEvent=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\ChatGPTQuotaOverlay.Stop");stopEvent.Reset();
      stopWait=ThreadPool.RegisterWaitForSingleObject(stopEvent,delegate {if(!closing)Dispatcher.BeginInvoke(new Action(Close));},null,-1,true);
      tracker.Interval=TimeSpan.FromMilliseconds(200); tracker.Tick+=delegate { UpdatePlacement(); };tracker.Start();
      motionTracker.Interval=TimeSpan.FromMilliseconds(16);
      motionTracker.Tick+=delegate {
        if(closing||NowMilliseconds()>=motionDeadline) {interactiveMove=false;motionTracker.Stop();QueuePlacementRefresh();return;}
        SyncTargetFrame();
      };
      // Out-of-context events run on this registering UI thread. Apply geometry
      // directly here; queuing the heavyweight refresh leaves a visible trail.
      nativeEvent=ReceiveWindowEvent;
      eventHook=Native.SetWinEventHook(3,3,IntPtr.Zero,nativeEvent,0,0,2);
      locationHook=Native.SetWinEventHook(0x800B,0x800B,IntPtr.Zero,nativeEvent,0,0,2);
      moveHook=Native.SetWinEventHook(0xA,0xB,IntPtr.Zero,nativeEvent,0,0,2);
      minimizeHook=Native.SetWinEventHook(0x16,0x17,IntPtr.Zero,nativeEvent,0,0,2);
      StartupCheckpoint("constructed");
    }
    static long NowMilliseconds() {return (long)(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency);}
    bool MotionActive() {return motionTracker.IsEnabled&&NowMilliseconds()<motionDeadline;}
    void QueuePlacementRefresh() {
      if(closing||refreshQueued)return;
      refreshQueued=true;
      Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate {refreshQueued=false;UpdatePlacement();}));
    }
    void ReceiveWindowEvent(IntPtr hook,uint ev,IntPtr window,int obj,int child,uint thread,uint time) {
      if(closing)return;
      if(ev==3){QueuePlacementRefresh();return;}
      if(window!=target||target==IntPtr.Zero||obj!=0||child!=0)return;
      if(ev==0xA)interactiveMove=true;
      if(ev==0xB) {
        interactiveMove=false;motionTracker.Stop();SyncTargetFrame();QueuePlacementRefresh();return;
      }
      if(ev==0x800B||ev==0xA) {
        motionDeadline=NowMilliseconds()+(interactiveMove?1000:120);
        SyncTargetFrame();motionTracker.Start();
      }else if(ev==0x16||ev==0x17){SyncTargetFrame();QueuePlacementRefresh();}
    }
    void StopTracking() {
      tracker.Stop();motionTracker.Stop();interactiveMove=false;
      foreach(IntPtr hook in new[]{eventHook,locationHook,moveHook,minimizeHook})if(hook!=IntPtr.Zero)Native.UnhookWinEvent(hook);
      eventHook=locationHook=moveHook=minimizeHook=IntPtr.Zero;
    }
    void StartupCheckpoint(string stage) {
      try {File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(stateFile)),"startup.json"),json.Serialize(new {process=Process.GetCurrentProcess().Id,executable=Process.GetCurrentProcess().MainModule.FileName,time=DateTime.UtcNow.ToString("o"),stage=stage,hwnd=hwnd.ToInt64(),previewError=previewError,stateWriteError=stateWriteError,receivedUpdates=receivedUpdates,statePath=stateFile,stateUpdated=File.GetLastWriteTimeUtc(stateFile).ToString("o"),previewUpdated=File.GetLastWriteTimeUtc(Path.Combine(Path.GetDirectoryName(stateFile),"panel.png")).ToString("o"),monitorState=diagnosticState}),Encoding.UTF8);}catch{}
    }
    static SolidColorBrush Brush(string color) { var b=(SolidColorBrush)new BrushConverter().ConvertFromString(color);b.Freeze();return b; }
    static TextBlock Text(string text,double size,FontWeight weight,string color) {
      return new TextBlock { Text=text,FontSize=size,FontWeight=weight,Foreground=Brush(color),TextAlignment=TextAlignment.Center,HorizontalAlignment=HorizontalAlignment.Stretch,TextWrapping=TextWrapping.NoWrap };
    }
    void ApplyMenuPalette() {
      optionsMenu.Background=Brush(palette.MenuSurface);optionsMenu.Foreground=Brush(palette.MenuText);optionsMenu.BorderBrush=Brush(palette.MenuBorder);
      string[] keys={"MenuSurface","MenuText","MenuBorder","MenuHover","MenuCheck","MenuArrow","MenuDivider"};
      string[] colors={palette.MenuSurface,palette.MenuText,palette.MenuBorder,palette.MenuHover,palette.MenuCheck,palette.MenuArrow,palette.MenuDivider};
      for(int i=0;i<keys.Length;i++)optionsMenu.Resources["Quota."+keys[i]]=Brush(colors[i]);
    }
    void ApplyTrayPalette() {
      if(tray.ContextMenuStrip==null)return;
      tray.ContextMenuStrip.BackColor=System.Drawing.ColorTranslator.FromHtml(palette.MenuSurface);
      tray.ContextMenuStrip.ForeColor=System.Drawing.ColorTranslator.FromHtml(palette.MenuText);
      foreach(Forms.ToolStripItem item in tray.ContextMenuStrip.Items)item.ForeColor=tray.ContextMenuStrip.ForeColor;
    }
    void SelectTheme(string mode) {
      if(mode!="dark"&&mode!="light"&&mode!="auto")return;
      themeMode=mode;SavePreferences();RefreshTheme(true);SyncMenu();lastState="";UpdatePlacement();
    }
    string T(string key) {return PanelLanguage.Text(language,key);}
    MenuItem Menu(string key,bool checkable=false) {return new MenuItem {Header=T(key),Tag=key,IsCheckable=checkable};}
    void TranslateMenu(ItemsControl items) {
      foreach(object child in items.Items) {
        var item=child as MenuItem;if(item==null)continue;
        if(item.Tag is string)item.Header=T((string)item.Tag);TranslateMenu(item);
      }
    }
    void SelectLanguage(string selected) {
      if(selected!="ru"&&selected!="en")return;
      language=selected;
      try {Preferences.Update(settingsFile,preferences=>{preferences["language"]=language;});}catch{}
      LocalizeRows();SyncMenu();ApplyTrayLanguage();SavePreview();lastState="";UpdatePlacement();
    }
    void LocalizeRows() {
      var message=view as Dictionary<string,object>;var rows=message!=null&&message.ContainsKey("rows")?message["rows"] as object[]:null;
      var tooltip=new StringBuilder();
      for(int i=0;i<2;i++) {
        var row=rows!=null&&rows.Length==2?rows[i] as Dictionary<string,object>:null;
        string original=row==null?T(i==0?"shortLabel":"weekLabel"):Get(row,"period");
        periods[i].Text=PanelLanguage.Period(language,original,i);
        markers[i].Text=row!=null&&Get(row,"marker").Length>0?T("stale"):"";
        if(i>0)tooltip.Append(" | ");tooltip.Append(periods[i].Text+": "+values[i].Text+" "+times[i].Text);
      }
      tray.Text=tooltip.ToString().Substring(0,Math.Min(63,tooltip.Length));UpdateCompactStatus();
    }
    void ApplyTrayLanguage() {
      pause.Text=T(paused?"resume":"pause");
      if(tray.ContextMenuStrip==null)return;
      tray.ContextMenuStrip.Items[0].Text=T("refreshNow");tray.ContextMenuStrip.Items[3].Text=T("close");
      if(taskbarTrayItem!=null){taskbarTrayItem.Text=T("taskbarShow");taskbarTraySides.Text=T("taskbar");taskbarTrayLeft.Text=T("taskbarLeft");taskbarTrayRight.Text=T("taskbarRight");}
    }
    bool RefreshTheme(bool force) {
      if(!force&&DateTime.UtcNow<nextThemeCheck)return false;
      nextThemeCheck=DateTime.UtcNow.AddSeconds(1);
      string resolved=themeResolver.Resolve(themeMode);
      if(resolved==effectiveTheme)return false;
      effectiveTheme=resolved;palette=resolved=="light"?ThemePalette.Light:ThemePalette.Dark;
      Background=TaskbarAppearance.InputSurface(backgroundTransparency);panel.Background=Brush(palette.Background(backgroundTransparency));panel.BorderBrush=Brush(palette.Border);divider.Background=Brush(palette.Divider);
      compactStale.Foreground=Brush(palette.Secondary);
      for(int i=0;i<2;i++) {
        periods[i].Foreground=Brush(palette.Secondary);markers[i].Foreground=Brush(palette.Secondary);
        values[i].Foreground=Brush(palette.Tone(rowTones[i]));times[i].Foreground=values[i].Foreground;
        dates[i].Foreground=i==1&&compact?values[i].Foreground:Brush(palette.Date);
        progressTracks[i].Background=Brush(palette.Track);
      }
      ApplyMenuPalette();ApplyTrayPalette();UpdateProgress();SyncTaskbar();SavePreview();lastState="";return true;
    }
    IntPtr WindowProc(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled) {
      if(message==0x21) {handled=true;return new IntPtr(3);} // MA_NOACTIVATE
      if(message==0x84) {handled=true;return new IntPtr(1);} // HTCLIENT: accept the panel's own clicks
      return IntPtr.Zero;
    }
    Dictionary<string,object> ReadPreferences() {
      try {return Preferences.Read(settingsFile);}
      catch {return new Dictionary<string,object>();}
    }
    static bool BooleanPreference(Dictionary<string,object> preferences,string key,bool fallback) {
      return preferences.ContainsKey(key) && preferences[key] is bool?(bool)preferences[key]:fallback;
    }
    void SavePreferences() {
      try {
        Preferences.Update(settingsFile,preferences=>{preferences["compact"]=preferCompact;preferences["showFullProgress"]=showFullProgress;
        preferences["displayMode"]=preferredMode;
        preferences["themeMode"]=themeMode;
        bool contour=fullProgressMode=="outline"||fullProgressMode=="both";
        preferences["showUltraProgress"]=contour;
        preferences["showCompactProgress"]=contour;
        preferences["fullProgressMode"]=fullProgressMode;
        preferences["indicatorMode"]=fullProgressMode;
        preferences["contourWindow"]=contourWindow;
        foreach(string key in new[]{"fullProgressWindow","compactProgressWindow","ultraProgressWindow"})preferences[key]=contourWindow;
        });
      }catch{}
    }
    void SetTaskbarEnabled(bool enabled) {
      bool previous=taskbarEnabled;taskbarEnabled=enabled;
      try{Preferences.Update(settingsFile,p=>{p["taskbarEnabled"]=taskbarEnabled;p["taskbarSide"]=taskbarSide;});}catch{taskbarEnabled=previous;SyncMenu();return;}
      SyncTaskbar();SyncMenu();lastState="";
      if(started)Command(enabled?"taskbar-on":"taskbar-off");
    }
    void SetTaskbarSide(string side) {
      if(side!="left"&&side!="right")return;
      string previous=taskbarSide;taskbarSide=side;
      try{Preferences.Update(settingsFile,p=>{p["taskbarEnabled"]=taskbarEnabled;p["taskbarSide"]=taskbarSide;});}catch{taskbarSide=previous;SyncMenu();return;}
      SyncTaskbar();SyncMenu();lastState="";
    }
    void SetBackgroundTransparency(int value) {
      value=Math.Max(0,Math.Min(100,value));
      if(closing||value==backgroundTransparency)return;
      try{Preferences.Update(settingsFile,p=>{p["backgroundTransparency"]=value;});}catch{SyncTransparencyControl();return;}
      backgroundTransparency=value;Background=TaskbarAppearance.InputSurface(value);panel.Background=Brush(palette.Background(value));
      SyncTaskbar();lastState="";
    }
    void SyncTransparencyControl() {
      if(transparencySlider==null)return;
      syncingTransparency=true;
      try{transparencySlider.Value=backgroundTransparency;transparencyLabel.Text=T("backgroundTransparency")+": "+backgroundTransparency+"%";transparencyLabel.Foreground=Brush(palette.MenuText);}
      finally{syncingTransparency=false;}
    }
    string TaskbarSideKey(bool last) {return taskbarWindow!=null&&taskbarWindow.IsVertical?(last?"taskbarBottom":"taskbarTop"):(last?"taskbarRight":"taskbarLeft");}
    void DisposeTaskbarView() {
      var previousWindow=taskbarWindow;var previousMenu=taskbarMenu;var previousWatcher=taskbarMenuDismiss;
      // Retired popup callbacks must never operate on a replacement's observer.
      taskbarWindow=null;taskbarMenu=null;taskbarMenuDismiss=null;transparencySlider=null;transparencyLabel=null;
      if(previousWatcher!=null)previousWatcher.Dispose();
      if(previousMenu!=null)previousMenu.IsOpen=false;
      if(previousWindow!=null){previousWindow.ContextMenu=null;previousWindow.Dispose();}
    }
    void SyncTaskbar() {
      if(closing)return;
      if(taskbarWindow!=null&&taskbarWindow.NeedsRecreation) {
        // Native attachment and menu construction belong to ordinary tracking,
        // never to the frame-rate main-panel movement path.
        if(MotionActive())return;
        DisposeTaskbarView();
      }
      if(taskbarWindow==null&&taskbarEnabled) {
        taskbarWindow=new TaskbarWindow(taskbarSnapshotReader);
        taskbarWindow.IsOtherMenuOpen=delegate {return optionsMenu.IsOpen||(tray.ContextMenuStrip!=null&&tray.ContextMenuStrip.Visible);};
        taskbarMenu=new ContextMenu {StaysOpen=false,Resources=optionsMenu.Resources,Template=optionsMenu.Template,Background=Brush(palette.MenuSurface),Foreground=Brush(palette.MenuText)};
        var enabled=Menu("taskbarShow",true);enabled.Click+=delegate {SetTaskbarEnabled(enabled.IsChecked);};
        var left=Menu("taskbarLeft",true);left.Click+=delegate {SetTaskbarSide("left");};
        var right=Menu("taskbarRight",true);right.Click+=delegate {SetTaskbarSide("right");};
        transparencyLabel=new TextBlock {FontSize=12,Margin=new Thickness(0,0,0,5)};
        transparencySlider=new Slider {Minimum=0,Maximum=100,Value=backgroundTransparency,Width=195,TickFrequency=1,IsSnapToTickEnabled=true,IsMoveToPointEnabled=true,Focusable=false};
        TaskbarAppearance.ApplySliderStyle(transparencySlider);
        transparencySlider.ValueChanged+=delegate {if(!syncingTransparency)SetBackgroundTransparency((int)Math.Round(transparencySlider.Value));};
        var transparencyContent=new StackPanel();transparencyContent.Children.Add(transparencyLabel);transparencyContent.Children.Add(transparencySlider);
        var transparencyItem=new MenuItem {Header=transparencyContent,StaysOpenOnClick=true};
        var stop=Menu("stop");stop.Click+=delegate {Close();};
        taskbarMenu.Items.Add(enabled);taskbarMenu.Items.Add(left);taskbarMenu.Items.Add(right);taskbarMenu.Items.Add(new Separator());taskbarMenu.Items.Add(transparencyItem);taskbarMenu.Items.Add(new Separator());taskbarMenu.Items.Add(stop);
        var createdMenu=taskbarMenu;
        var createdWatcher=new MenuDismissWatcher(Dispatcher,delegate{return !closing&&Object.ReferenceEquals(taskbarMenu,createdMenu)&&createdMenu.IsOpen;},delegate{createdMenu.IsOpen=false;},null);
        taskbarMenuDismiss=createdWatcher;
        createdMenu.Opened+=delegate {if(closing||!Object.ReferenceEquals(taskbarMenu,createdMenu))return;TranslateMenu(createdMenu);left.Header=T(TaskbarSideKey(false));right.Header=T(TaskbarSideKey(true));SyncTransparencyControl();enabled.IsChecked=taskbarEnabled;left.IsChecked=taskbarSide=="left";right.IsChecked=taskbarSide=="right";createdWatcher.Start();};
        createdMenu.Closed+=delegate {createdWatcher.Stop();};taskbarWindow.ContextMenu=createdMenu;
      }
      if(taskbarWindow==null)return;
      taskbarWindow.Configure(taskbarEnabled&&!paused,taskbarSide,palette,backgroundTransparency);SyncTransparencyControl();
      bool stale=markers[0].Text.Length>0||markers[1].Text.Length>0;
      taskbarWindow.ApplyRows(values[0].Text,values[1].Text,rowTones[0],rowTones[1],stale);
    }
    void SetupPanelMenu() {
      optionsMenu=new ContextMenu {StaysOpen=false,Background=Brush(palette.MenuSurface),Foreground=Brush(palette.Primary),BorderBrush=Brush(palette.MenuBorder),BorderThickness=new Thickness(1),FontSize=12,Padding=new Thickness(4),Placement=System.Windows.Controls.Primitives.PlacementMode.Right,PlacementTarget=panel,HorizontalOffset=8};
      ApplyMenuPalette();
      ContextMenuService.SetPlacement(panel,System.Windows.Controls.Primitives.PlacementMode.Right);
      ContextMenuService.SetPlacementTarget(panel,panel);ContextMenuService.SetHorizontalOffset(panel,8);
      optionsMenu.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"
        <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ContextMenu'>
          <Border Background='{DynamicResource Quota.MenuSurface}' BorderBrush='{DynamicResource Quota.MenuBorder}' BorderThickness='1' CornerRadius='8' Padding='4'>
            <StackPanel IsItemsHost='True'/>
          </Border>
        </ControlTemplate>");
      var style=new Style(typeof(MenuItem));style.Setters.Add(new Setter(Control.ForegroundProperty,new DynamicResourceExtension("Quota.MenuText")));
      style.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"
        <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='MenuItem'>
          <Border x:Name='ItemBackground' CornerRadius='4' Padding='8,6' Background='Transparent'>
            <Grid>
              <Grid.ColumnDefinitions><ColumnDefinition Width='20'/><ColumnDefinition Width='*'/><ColumnDefinition Width='18'/></Grid.ColumnDefinitions>
              <TextBlock x:Name='Check' Text='✓' Foreground='{DynamicResource Quota.MenuCheck}' FontSize='13' Visibility='Collapsed' VerticalAlignment='Center'/>
              <ContentPresenter Grid.Column='1' ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center'/>
              <TextBlock x:Name='Arrow' Grid.Column='2' Text='›' Foreground='{DynamicResource Quota.MenuArrow}' FontSize='17' HorizontalAlignment='Right' VerticalAlignment='Center' Visibility='Collapsed'/>
              <Popup x:Name='PART_Popup' AllowsTransparency='True' Focusable='False' Placement='Right' PlacementTarget='{Binding RelativeSource={RelativeSource TemplatedParent}}' IsOpen='{Binding IsSubmenuOpen,RelativeSource={RelativeSource TemplatedParent}}'>
                <Border Background='{DynamicResource Quota.MenuSurface}' BorderBrush='{DynamicResource Quota.MenuBorder}' BorderThickness='1' CornerRadius='8' Padding='4'>
                  <StackPanel IsItemsHost='True' MinWidth='125'/>
                </Border>
              </Popup>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='ItemBackground' Property='Background' Value='{DynamicResource Quota.MenuHover}'/></Trigger>
            <Trigger Property='IsChecked' Value='True'><Setter TargetName='Check' Property='Visibility' Value='Visible'/></Trigger>
            <Trigger Property='HasItems' Value='True'><Setter TargetName='Arrow' Property='Visibility' Value='Visible'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter TargetName='ItemBackground' Property='Opacity' Value='.45'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>")));
      optionsMenu.Resources.Add(typeof(MenuItem),style);
      var separatorStyle=new Style(typeof(Separator));separatorStyle.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"
        <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Separator'>
          <Border Height='1' Margin='8,4' Background='{DynamicResource Quota.MenuDivider}'/>
        </ControlTemplate>")));optionsMenu.Resources.Add(typeof(Separator),separatorStyle);
      var refresh=Menu("refresh");refresh.Click+=delegate {Command("refresh");};optionsMenu.Items.Add(refresh);
      updatesItem=Menu("checkUpdates");updatesItem.Click+=delegate {
        if(!updatesItem.IsEnabled)return;
        updatesItem.IsEnabled=false;updatesItem.Header=T("checkingUpdates");Command("check-updates");
      };optionsMenu.Items.Add(updatesItem);
      var modes=Menu("view");
      fullModeItem=Menu("full",true);fullModeItem.Click+=delegate {SelectMode("full");};
      compactModeItem=Menu("compact",true);compactModeItem.Click+=delegate {SelectMode("compact");};
      ultraModeItem=Menu("ultra",true);ultraModeItem.Click+=delegate {SelectMode("ultra");};
      modes.Items.Add(fullModeItem);modes.Items.Add(compactModeItem);modes.Items.Add(ultraModeItem);optionsMenu.Items.Add(modes);
      var taskbar=Menu("taskbar");
      taskbarEnabledItem=Menu("taskbarShow",true);taskbarEnabledItem.Click+=delegate {SetTaskbarEnabled(taskbarEnabledItem.IsChecked);};
      taskbarLeftItem=Menu("taskbarLeft",true);taskbarLeftItem.Click+=delegate {SetTaskbarSide("left");};
      taskbarRightItem=Menu("taskbarRight",true);taskbarRightItem.Click+=delegate {SetTaskbarSide("right");};
      taskbarStatusItem=new MenuItem {IsEnabled=false};
      taskbar.Items.Add(taskbarEnabledItem);taskbar.Items.Add(taskbarLeftItem);taskbar.Items.Add(taskbarRightItem);taskbar.Items.Add(taskbarStatusItem);optionsMenu.Items.Add(taskbar);
      var themes=Menu("theme");
      themeDarkItem=Menu("dark",true);themeDarkItem.Click+=delegate {SelectTheme("dark");};
      themeLightItem=Menu("light",true);themeLightItem.Click+=delegate {SelectTheme("light");};
      themeAutoItem=Menu("auto",true);themeAutoItem.Click+=delegate {SelectTheme("auto");};
      themes.Items.Add(themeDarkItem);themes.Items.Add(themeLightItem);themes.Items.Add(themeAutoItem);optionsMenu.Items.Add(themes);
      var languages=Menu("language");languageRussianItem=Menu("ru",true);languageEnglishItem=Menu("en",true);
      languageRussianItem.Click+=delegate {SelectLanguage("ru");};languageEnglishItem.Click+=delegate {SelectLanguage("en");};
      languages.Items.Add(languageRussianItem);languages.Items.Add(languageEnglishItem);optionsMenu.Items.Add(languages);
      autostartItem=Menu("autostart",true);autostartItem.Click+=delegate {SetAutostart(autostartItem.IsChecked);};optionsMenu.Items.Add(autostartItem);
      optionsMenu.Items.Add(new Separator {Style=separatorStyle});
      var fullSettings=Menu("indicator");
      fullProgressItem=Menu("bars",true);fullProgressItem.Click+=delegate {SelectFullProgress("bars");};
      fullOutlineItem=Menu("outline",true);fullOutlineItem.Click+=delegate {SelectFullProgress("outline");};
      fullBothItem=Menu("both",true);fullBothItem.Click+=delegate {SelectFullProgress("both");};
      fullOffItem=Menu("off",true);fullOffItem.Click+=delegate {SelectFullProgress("off");};
      fullSettings.Items.Add(fullProgressItem);fullSettings.Items.Add(fullOutlineItem);fullSettings.Items.Add(fullBothItem);fullSettings.Items.Add(fullOffItem);
      optionsMenu.Items.Add(fullSettings);
      contourSourceItem=Menu("source");
      shortProgressItem=Menu("short",true);weeklyProgressItem=Menu("weekly",true);
      shortProgressItem.Click+=delegate {contourWindow="short";SavePreferences();UpdateProgress();SavePreview();};
      weeklyProgressItem.Click+=delegate {contourWindow="weekly";SavePreferences();UpdateProgress();SavePreview();};
      contourSourceItem.Items.Add(shortProgressItem);contourSourceItem.Items.Add(weeklyProgressItem);optionsMenu.Items.Add(contourSourceItem);
      optionsMenu.Items.Add(new Separator {Style=separatorStyle});
      pauseUpdatesItem=Menu("pauseUpdates");pauseUpdatesItem.Click+=delegate {TogglePaused();};optionsMenu.Items.Add(pauseUpdatesItem);
      var stop=Menu("stop");stop.Click+=delegate {Close();};optionsMenu.Items.Add(stop);
      menuDismiss=new MenuDismissWatcher(Dispatcher,delegate {return !closing&&optionsMenu.IsOpen;},delegate {optionsMenu.IsOpen=false;},null);
      optionsMenu.Opened+=delegate {SyncMenu();menuDismiss.Start();};
      optionsMenu.Closed+=delegate {menuDismiss.Stop();};panel.ContextMenu=optionsMenu;
    }
    void SyncMenu() {
      TranslateMenu(optionsMenu);languageRussianItem.IsChecked=language=="ru";languageEnglishItem.IsChecked=language=="en";
      if(updatesItem!=null&&!updatesItem.IsEnabled)updatesItem.Header=T("checkingUpdates");
      taskbarEnabledItem.IsChecked=taskbarEnabled;taskbarLeftItem.IsChecked=taskbarSide=="left";taskbarRightItem.IsChecked=taskbarSide=="right";
      taskbarLeftItem.Header=T(TaskbarSideKey(false));taskbarRightItem.Header=T(TaskbarSideKey(true));
      taskbarStatusItem.Header=T(!taskbarEnabled?"taskbarOff":taskbarWindow!=null&&taskbarWindow.IsVisible?"taskbarActive":"taskbarWaiting");
      if(taskbarTrayItem!=null){taskbarTrayItem.Checked=taskbarEnabled;taskbarTrayLeft.Checked=taskbarSide=="left";taskbarTrayRight.Checked=taskbarSide=="right";}
      fullProgressItem.IsChecked=fullProgressMode=="bars";fullOutlineItem.IsChecked=fullProgressMode=="outline";fullOffItem.IsChecked=fullProgressMode=="off";
      fullBothItem.IsChecked=fullProgressMode=="both";
      shortProgressItem.IsChecked=contourWindow=="short";weeklyProgressItem.IsChecked=contourWindow=="weekly";
      fullModeItem.IsChecked=preferredMode=="full";compactModeItem.IsChecked=preferredMode=="compact";ultraModeItem.IsChecked=preferredMode=="ultra";
      themeDarkItem.IsChecked=themeMode=="dark";themeLightItem.IsChecked=themeMode=="light";themeAutoItem.IsChecked=themeMode=="auto";
      autostartItem.IsChecked=autoStartEnabled;autostartItem.IsEnabled=!autostartBusy;
      pauseUpdatesItem.Header=T(paused?"resumeUpdates":"pauseUpdates");
    }
    void SelectFullProgress(string mode) {
      fullProgressMode=mode;showFullProgress=mode!="off";SavePreferences();UpdateProgress();SavePreview();
    }
    void SetAutostart(bool enabled) {
      if(autostartBusy)return;autostartBusy=true;SyncMenu();
      ThreadPool.QueueUserWorkItem(delegate {
        bool succeeded=false,actual=autoStartEnabled;
        try {
          var info=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"QuotaMonitor.exe"),enabled?"--enable-autostart-json":"--disable-autostart-json") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
          using(var child=Process.Start(info)) {
            if(!child.WaitForExit(10000)){try{child.Kill();}catch{}throw new IOException("Autostart update timed out");}
            var result=new JavaScriptSerializer().DeserializeObject(child.StandardOutput.ReadToEnd()) as Dictionary<string,object>;
            if(child.ExitCode!=0||result==null||!result.ContainsKey("autoStartConfigured")||!(result["autoStartConfigured"] is bool)||!(bool)result["autoStartConfigured"])throw new IOException("Autostart update failed");
            actual=Convert.ToBoolean(result["autoStartEnabled"]);succeeded=true;
          }
        }catch{}
        Dispatcher.BeginInvoke(new Action(delegate {
          if(closing)return;autostartBusy=false;if(succeeded)autoStartEnabled=actual;SyncMenu();lastState="";
          if(!succeeded&&tray.Visible)tray.ShowBalloonTip(4000,"Quota Panel",T("autostartError"),Forms.ToolTipIcon.Warning);
        }));
      });
    }
    void TogglePaused() {
      paused=!paused;pause.Text=T(paused?"resume":"pause");Command(paused?"pause":"resume");SyncTaskbar();SyncMenu();UpdatePlacement();
    }
    void UpdateProgress() {
      if(progressOutline==null)return;
      bool ultra=presentationMode=="ultra";
      bool bars=ultra||fullProgressMode=="bars"||fullProgressMode=="both";
      bool contour=fullProgressMode=="outline"||fullProgressMode=="both";
      showCompactProgress=showUltraProgress=contour;
      bool compactBars=compact&&!ultra&&bars;
      panel.Padding=new Thickness(3,compactBars?2:compact?3:5,3,compactBars?2:compact?3:5);
      divider.Margin=new Thickness(1,compactBars?1:compact?3:5,1,compactBars?1:compact?3:5);
      for(int i=0;i<2;i++) {
        double fraction=IndicatorFraction(remainingQuota[i]);
        progressTracks[i].Visibility=bars?Visibility.Visible:Visibility.Collapsed;
        progressTracks[i].Height=ultra?4:compact?2:3;
        progressTracks[i].Margin=ultra?new Thickness(3,4,3,4):compact?new Thickness(3,1,3,0):new Thickness(3,3,3,1);
        progressFills[i].Height=progressTracks[i].Height;
        progressFills[i].Width=32*fraction;
        Brush colour=!remainingQuota[i].HasValue?Brush(palette.Secondary):remainingQuota[i]<=10?Brush(palette.Critical):remainingQuota[i]<=30?Brush(palette.Warning):Brush(i==0?palette.Short:palette.Weekly);
        progressFills[i].Background=colour;
        progressTracks[i].Opacity=markers[i].Text.Length>0?.45:1;
      }
      int selected=contourWindow=="weekly"?1:0;
      progressOutline.Height=panel.Height;
      progressOutline.Visibility=contour?Visibility.Visible:Visibility.Collapsed;
      progressOutline.Stroke=progressFills[selected].Background;progressOutline.Opacity=markers[selected].Text.Length>0?.45:1;
      progressOutline.Data=ProgressGeometry.Create(Layout.PanelWidth,panel.Height,IndicatorFraction(remainingQuota[selected]));
      SyncMenu();
    }
    static double IndicatorFraction(double? remaining) {
      // Known zero is a full red alarm, not a full quota. Keep numeric text at 0%.
      // Unknown data has no fill and must never masquerade as this alarm.
      return !remaining.HasValue?0:remaining.Value==0?1:remaining.Value/100;
    }
    void ToggleMode() {
      SelectMode(presentationMode=="full"?"compact":presentationMode=="compact"?"ultra":"full");
    }
    void SelectMode(string mode) {
      if(mode!="full" && mode!="compact" && mode!="ultra")return;
      preferredMode=mode;preferCompact=mode!="full";
      SavePreferences();
      lastState="";
      if(hwnd==IntPtr.Zero)SetMode(preferredMode);
      else UpdatePlacement();
    }
    void SetCompact(bool value) {
      SetMode(value?"compact":"full");
    }
    void SetMode(string mode) {
      presentationMode=mode;compact=mode!="full";bool ultra=mode=="ultra";
      panel.Height=ultra?Layout.UltraHeight:compact?Layout.CompactHeight:Layout.PanelHeight;
      // A live HWND is resized and repositioned together by ApplyNativePlacement.
      // Setting Window.Height here would resize first at its previous top edge.
      if(hwnd==IntPtr.Zero)Height=panel.Height;
      surface.Height=panel.Height;
      panel.Padding=new Thickness(3,compact?3:5,3,compact?3:5);
      divider.Margin=new Thickness(1,compact?3:5,1,compact?3:5);
      for(int i=0;i<2;i++) {
        values[i].Visibility=ultra?Visibility.Collapsed:Visibility.Visible;
        periods[i].Visibility=compact?Visibility.Collapsed:Visibility.Visible;
        markers[i].Visibility=compact?Visibility.Collapsed:Visibility.Visible;
      }
      divider.Visibility=ultra?Visibility.Collapsed:Visibility.Visible;
      times[0].Visibility=ultra?Visibility.Collapsed:Visibility.Visible;
      dates[1].Visibility=ultra?Visibility.Collapsed:Visibility.Visible;
      dates[0].Visibility=compact?Visibility.Collapsed:Visibility.Visible;
      times[1].Visibility=compact?Visibility.Collapsed:Visibility.Visible;
      dates[1].FontSize=compact?11.5:10.5;dates[1].FontWeight=compact?FontWeights.Bold:FontWeights.Normal;
      dates[1].Foreground=compact?values[1].Foreground:Brush(palette.Date);
      UpdateCompactStatus();UpdateProgress();
      if(hwnd==IntPtr.Zero)SavePreview();
    }
    void UpdateCompactStatus() {
      compactStale.Text=markers[0].Text.Length>0 || markers[1].Text.Length>0 ? T("stale") : "";
      compactStale.Visibility=presentationMode=="compact" && compactStale.Text.Length>0?Visibility.Visible:Visibility.Collapsed;
    }
    static void Command(string command) { try { Console.Out.WriteLine("{\"command\":\""+command+"\"}");Console.Out.Flush(); } catch {} }
    void SetupTray() {
      tray.Icon=System.Drawing.SystemIcons.Information;
      tray.Text=T("trayTitle");
      var menu=new Forms.ContextMenuStrip();
      menu.Items.Add(T("refreshNow"),null,delegate { Command("refresh"); });
      pause.Click+=delegate {TogglePaused();};
      menu.Items.Add(pause);
      menu.Items.Add(new Forms.ToolStripSeparator());
      menu.Items.Add(T("close"),null,delegate { Close(); });
      menu.Items.Add(new Forms.ToolStripSeparator());
      taskbarTrayItem=new Forms.ToolStripMenuItem(T("taskbarShow")) {CheckOnClick=true,Checked=taskbarEnabled};taskbarTrayItem.Click+=delegate {SetTaskbarEnabled(taskbarTrayItem.Checked);};
      taskbarTraySides=new Forms.ToolStripMenuItem(T("taskbar"));
      taskbarTrayLeft=new Forms.ToolStripMenuItem(T("taskbarLeft"));taskbarTrayLeft.Click+=delegate {SetTaskbarSide("left");};
      taskbarTrayRight=new Forms.ToolStripMenuItem(T("taskbarRight"));taskbarTrayRight.Click+=delegate {SetTaskbarSide("right");};
      taskbarTraySides.DropDownItems.Add(taskbarTrayLeft);taskbarTraySides.DropDownItems.Add(taskbarTrayRight);menu.Items.Add(taskbarTrayItem);menu.Items.Add(taskbarTraySides);
      tray.ContextMenuStrip=menu;ApplyTrayLanguage();ApplyTrayPalette();tray.Visible=true;
      menu.Opening+=delegate{taskbarTrayLeft.Text=T(TaskbarSideKey(false));taskbarTrayRight.Text=T(TaskbarSideKey(true));};
      tray.DoubleClick+=delegate {Command("refresh");};
    }
    void StartInput() {
      var thread=new Thread(delegate() {
        try { string line;while((line=Console.ReadLine())!=null) {
          var message=new JavaScriptSerializer().DeserializeObject(line) as Dictionary<string,object>;
          if(message!=null)Dispatcher.BeginInvoke(new Action(delegate { Apply(message); }));
        }} catch {} finally { if(!closing)Dispatcher.BeginInvoke(new Action(Close)); }
      });thread.IsBackground=true;thread.Start();Command("ready");if(taskbarEnabled)Command("taskbar-on");
    }
    void Apply(Dictionary<string,object> message) {
      object update;
      if(message.TryGetValue("update",out update)) {ApplyUpdate(update as Dictionary<string,object>);return;}
      if(!message.ContainsKey("rows"))return;
      receivedUpdates++;
      view=message;var rows=message["rows"] as object[];if(rows==null||rows.Length!=2)return;
      var tooltip=new StringBuilder();
      for(int i=0;i<2;i++) {
        var row=rows[i] as Dictionary<string,object>;if(row==null)continue;
        remainingQuota[i]=null;object numeric;
        if(row.TryGetValue("remaining",out numeric) && numeric!=null && !(numeric is string) && !(numeric is bool))try {
          double value=Convert.ToDouble(numeric);if(!Double.IsNaN(value)&&!Double.IsInfinity(value))remainingQuota[i]=Math.Max(0,Math.Min(100,value));
        }catch{}
        periods[i].Text=Get(row,"period");values[i].Text=Get(row,"value");dates[i].Text=Get(row,"date");times[i].Text=Get(row,"time");markers[i].Text=Get(row,"marker");
        rowTones[i]=Get(row,"tone");var color=Brush(palette.Tone(rowTones[i]));
        values[i].Foreground=color;times[i].Foreground=color;
        if(i==1)dates[i].Foreground=compact?color:Brush(palette.Date);
        if(i>0)tooltip.Append(" | ");tooltip.Append(periods[i].Text+": "+values[i].Text+" "+times[i].Text);
      }
      tray.Text=tooltip.ToString().Substring(0,Math.Min(63,tooltip.Length));
      LocalizeRows();UpdateProgress();SyncTaskbar();
      lastState="";
      if(MotionActive()) {
        pendingPreview=true;if(receivedUpdates<=30)pendingStartupCheckpoint=true;
        SyncTargetFrame();
      }else {
        SavePreview();UpdatePlacement();
        if(receivedUpdates<=30)StartupCheckpoint("received-data");
      }
    }
    static bool SafeReleaseUrl(string url) {
      return url!=null&&url.Length<200&&System.Text.RegularExpressions.Regex.IsMatch(url,@"\Ahttps://github\.com/MrBogomoll/[A-Za-z0-9][A-Za-z0-9_.-]{0,99}/releases/tag/v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)\z");
    }
    void ApplyUpdate(Dictionary<string,object> update) {
      if(closing||update==null)return;
      string state=Get(update,"state");
      if(state=="checking"){updatesItem.IsEnabled=false;updatesItem.Header=T("checkingUpdates");return;}
      updatesItem.IsEnabled=true;updatesItem.Header=T("checkUpdates");
      string current=Get(update,"currentVersion"),latest=Get(update,"latestVersion"),url=Get(update,"url"),reason=Get(update,"reason");
      bool available=state=="available"&&SafeReleaseUrl(url);
      string text=available?String.Format(T("updateAvailable"),latest,current):state=="current"?String.Format(T("updateCurrent"),current):state=="ahead"?String.Format(T("updateAhead"),current,latest):T(reason=="rate-limit"?"updateRateLimit":reason=="timeout"?"updateTimeout":"updateError");
      if(updateDialog!=null)updateDialog.Close();
      var dialog=new Window {Title=T("updatesTitle"),Width=400,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Brush(palette.MenuSurface),Foreground=Brush(palette.MenuText),FontFamily=FontFamily,FontSize=14};
      if(hwnd!=IntPtr.Zero)dialog.Owner=this;
      var body=new StackPanel {Margin=new Thickness(20)};dialog.Content=body;
      var label=new TextBlock {Text=text,TextWrapping=TextWrapping.Wrap,Foreground=Brush(palette.MenuText),Margin=new Thickness(0,0,0,18)};body.Children.Add(label);
      var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};body.Children.Add(buttons);
      if(available) {
        var open=new Button {Content=T("openRelease"),Padding=new Thickness(12,6,12,6),Margin=new Thickness(0,0,8,0),Background=Brush(palette.MenuSurface),Foreground=Brush(palette.MenuText),BorderBrush=Brush(palette.MenuBorder)};
        open.Click+=delegate {try{openReleasePage(url);}catch{label.Text=T("openReleaseError");}};buttons.Children.Add(open);
      }
      var close=new Button {Content=T("dismiss"),Padding=new Thickness(12,6,12,6),Background=Brush(palette.MenuSurface),Foreground=Brush(palette.MenuText),BorderBrush=Brush(palette.MenuBorder),IsCancel=true};close.Click+=delegate {dialog.Close();};buttons.Children.Add(close);
      dialog.Closed+=delegate {if(updateDialog==dialog)updateDialog=null;};updateDialog=dialog;showUpdateDialog(dialog);
    }
    static string Get(Dictionary<string,object> row,string key) { return row.ContainsKey(key)?Convert.ToString(row[key]):""; }
    bool IsTarget(IntPtr window) {
      if(targetMatcher!=null)return targetMatcher(window);
      if(window==IntPtr.Zero||!Native.IsWindowVisible(window))return false;
      uint id;Native.GetWindowThreadProcessId(window,out id);
      try {
        using(var p=Process.GetProcessById((int)id)) {
          if(!p.ProcessName.Equals("ChatGPT",StringComparison.OrdinalIgnoreCase))return false;
          string file=p.MainModule.FileName;
          return file.IndexOf("\\WindowsApps\\OpenAI.Codex_",StringComparison.OrdinalIgnoreCase)>=0;
        }
      } catch {return false;} // Retry transient startup failures; PIDs may also be reused.
    }
    sealed class TargetFrame {
      internal bool Valid,AppPresent,AppReady,Show;
      internal double Scale;
      internal Native.Rect Rect;
      internal Native.Point Origin;
      internal Placement Placement;
    }
    TargetFrame SyncTargetFrame() {
      if(closing||hwnd==IntPtr.Zero||placing)return null;
      placing=true;
      try {
      Native.Rect rect=new Native.Rect();var origin=new Native.Point();int cloaked=0;
      bool valid=target!=IntPtr.Zero && Native.IsWindow(target) && Native.IsWindowVisible(target) && !Native.IsIconic(target);
      if(valid) {Native.DwmGetWindowAttribute(target,14,out cloaked,4);valid=cloaked==0&&Native.GetClientRect(target,out rect)&&Native.ClientToScreen(target,ref origin);}
      bool appPresent=target!=IntPtr.Zero&&Native.IsWindow(target)&&Native.IsWindowVisible(target);
      if(appPresent){Native.DwmGetWindowAttribute(target,14,out cloaked,4);appPresent=cloaked==0;}
      bool appReady=appStartup.Ready(target,appPresent,NowMilliseconds());
      double scale=valid?Native.GetDpiForWindow(target)/96.0:1;
      var placement=Layout.Place(origin.X,origin.Y,rect.Right-rect.Left,rect.Bottom-rect.Top,scale,preferredMode);
      bool modeChanged=presentationMode!=placement.Mode;
      if(modeChanged)SetMode(placement.Mode);
      bool show=valid&&!paused&&placement.Visible&&appReady;
      if(show) {
        if(!IsVisible) Show();
        ApplyAttachedPlacement(hwnd,target,placement,optionsMenu.IsOpen);
      } else if(IsVisible)Hide();
      if(modeChanged)pendingPreview=true;
      return new TargetFrame {Valid=valid,AppPresent=appPresent,AppReady=appReady,Show=show,Scale=scale,Rect=rect,Origin=origin,Placement=placement};
      }finally{placing=false;}
    }
    void UpdatePlacement() {
      if(closing||hwnd==IntPtr.Zero)return;
      // Discovery, theme reads, previews and diagnostics stay off the drag path.
      if(MotionActive()){SyncTargetFrame();return;}
      if((taskbarWindow!=null&&taskbarWindow.NeedsRecreation)||(taskbarEnabled&&taskbarWindow==null))SyncTaskbar();
      RefreshTheme(false);
      IntPtr foreground=Native.GetForegroundWindow();
      IntPtr root=Native.GetAncestor(foreground,3);
      if(IsTarget(root))target=root;
      else if(target==IntPtr.Zero||!Native.IsWindow(target)) {
        Native.EnumWindows(delegate(IntPtr w,IntPtr p){ if(IsTarget(w)) {var title=new StringBuilder(256);Native.GetWindowText(w,title,256);if(title.Length>0){target=w;return false;}}return true;},IntPtr.Zero);
      }
      var frame=SyncTargetFrame();if(frame==null)return;
      bool valid=frame.Valid,appPresent=frame.AppPresent,appReady=frame.AppReady,show=frame.Show;
      double scale=frame.Scale;var rect=frame.Rect;var origin=frame.Origin;var placement=frame.Placement;
      if(!appSignalSent||lastAppReady!=appReady){appSignalSent=true;lastAppReady=appReady;Command(appReady?"app-open":"app-closed");}
      if(pendingPreview) {pendingPreview=false;UpdateLayout();SavePreview();}
      IntPtr owner=Native.GetWindowLongPtr(hwnd,-8);uint ownerProcess=0;if(owner!=IntPtr.Zero)Native.GetWindowThreadProcessId(owner,out ownerProcess);
      var state=new {backgroundTransparency=backgroundTransparency,taskbar=new {enabled=taskbarEnabled,side=taskbarSide,visible=taskbarWindow!=null&&taskbarWindow.IsVisible,status=taskbarWindow==null?null:taskbarWindow.PlacementStatus},targetVisible=valid,topmost=(Native.GetWindowLongPtr(hwnd,-20).ToInt64()&8)!=0,targetTopmost=(Native.GetWindowLongPtr(target,-20).ToInt64()&8)!=0,language=language,appReady=appReady,appPresent=appPresent,startupDelayMs=AppStartupGate.DelayMilliseconds,autoStartEnabled=autoStartEnabled,visible=show,paused=paused,compact=compact,preferredCompact=preferCompact,mode=presentationMode,preferredMode=preferredMode,theme=new {mode=themeMode,effective=effectiveTheme,appPreference=themeResolver.AppPreference,source=themeResolver.Source},target=target.ToInt64(),foreground=foreground.ToInt64(),owner=owner.ToInt64(),ownerProcess=ownerProcess,process=Process.GetCurrentProcess().Id,dpi=scale*96,client=new {x=origin.X,y=origin.Y,width=rect.Right-rect.Left,height=rect.Bottom-rect.Top},panel=new {x=placement.X,y=placement.Y,width=placement.Width,height=placement.Height},noActivate=(Native.GetWindowLongPtr(hwnd,-20).ToInt64()&0x08000000)!=0,clickThrough=(Native.GetWindowLongPtr(hwnd,-20).ToInt64()&0x20)!=0,clickToToggle=true,hoverTooltipEnabled=ToolTipService.GetIsEnabled(panel),progress=new {indicatorMode=fullProgressMode,fullEnabled=showFullProgress,fullMode=fullProgressMode,fullWindow=contourWindow,compactEnabled=showCompactProgress,compactWindow=contourWindow,ultraEnabled=showUltraProgress,ultraWindow=contourWindow,shortRemaining=remainingQuota[0],weeklyRemaining=remainingQuota[1]},view=view};
      string encoded=json.Serialize(state);
      diagnosticState=state;
      if(encoded!=lastState){lastState=encoded;try{File.WriteAllText(stateFile,encoded,Encoding.UTF8);stateWriteError="";}catch(Exception error){stateWriteError=error.GetType().Name+":"+error.HResult.ToString();}}
      if(pendingStartupCheckpoint){pendingStartupCheckpoint=false;StartupCheckpoint("received-data");}
    }
    internal static void ApplyNativePlacement(IntPtr handle,Placement placement,bool menuOpen) {
      PlaceWindow(handle,IntPtr.Zero,placement,menuOpen);
    }
    internal static void ApplyAttachedPlacement(IntPtr handle,IntPtr target,Placement placement,bool menuOpen) {
      IntPtr after=IntPtr.Zero;
      if(!menuOpen&&target!=IntPtr.Zero) {
        // Follow the target's actual layer, including while another app has focus.
        // No cross-process owner/parent or input-queue attachment is necessary.
        after=Native.GetWindow(target,3); // GW_HWNDPREV: the window above ChatGPT.
        if(after==handle)after=Native.GetWindow(handle,3);
        bool pinned=(Native.GetWindowLongPtr(target,-20).ToInt64()&8)!=0;
        if(after==IntPtr.Zero||(!pinned&&(Native.GetWindowLongPtr(after,-20).ToInt64()&8)!=0)) {
          // Never promote a normal panel by inserting it behind a topmost window.
          // HWND_NOTOPMOST is needed only to demote an earlier pinned state.
          after=pinned?new IntPtr(-1):(Native.GetWindowLongPtr(handle,-20).ToInt64()&8)!=0?new IntPtr(-2):IntPtr.Zero;
        }
      }
      PlaceWindow(handle,after,placement,menuOpen);
    }
    static void PlaceWindow(IntPtr handle,IntPtr after,Placement placement,bool menuOpen) {
      uint flags=0x10|0x40|0x200; // no activation, show, preserve owner order.
      if(menuOpen)flags|=0x4; // SWP_NOZORDER: never raise our panel over an open menu.
      Native.SetWindowPos(handle,after,(int)Math.Round(placement.X),(int)Math.Round(placement.Y),(int)Math.Round(placement.Width),(int)Math.Round(placement.Height),flags);
    }
    void SavePreview() {
      // Capture only our own WPF visual, never the desktop or another application.
      try {
        double height=panel.Height;
        // Never rearrange the attached visual for a diagnostic image. Doing so
        // overwrites its live bottom offset and breaks the parent's layout cache.
        if(PresentationSource.FromVisual(surface)==null) {
          surface.Measure(new Size(Layout.PanelWidth,height));surface.Arrange(new Rect(0,0,Layout.PanelWidth,height));
        }
        surface.UpdateLayout();
        var snapshot=new DrawingVisual();
        using(var drawing=snapshot.RenderOpen())drawing.DrawRectangle(new VisualBrush(surface) {Stretch=Stretch.Fill,AutoLayoutContent=false},null,new Rect(0,0,Layout.PanelWidth,height));
        var image=new RenderTargetBitmap((int)Layout.PanelWidth*3,(int)height*3,288,288,PixelFormats.Pbgra32);image.Render(snapshot);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using(var stream=File.Create(Path.Combine(Path.GetDirectoryName(stateFile),"panel.png")))encoder.Save(stream);previewError="";
      }catch(Exception error){previewError=error.GetType().Name+":"+error.HResult.ToString();}
    }
  }
  public static class Program {
    [STAThread] public static void Main(string[] args) {
      Console.SetIn(new StreamReader(Console.OpenStandardInput(),new UTF8Encoding(false)));
      Console.SetOut(new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)) { AutoFlush=true });
      bool created;using(var mutex=new Mutex(true,"Local\\ChatGPTQuotaOverlay.v1",out created)) {
        if(!created)return;
        string state=args.Length>0?args[0]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"state.json");
        var app=new Application { ShutdownMode=ShutdownMode.OnMainWindowClose };
        app.Run(new OverlayWindow(state));
      }
    }
  }
}
