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
    readonly Forms.NotifyIcon tray=new Forms.NotifyIcon();
    readonly Forms.ToolStripMenuItem pause=new Forms.ToolStripMenuItem("Приостановить");
    Native.EventProc nativeEvent;
    IntPtr eventHook,hwnd,target;
    EventWaitHandle stopEvent;
    RegisteredWaitHandle stopWait;
    bool paused,closing,started,preferCompact,compact;
    string preferredMode,presentationMode;
    string themeMode,effectiveTheme;
    ThemePalette palette;
    readonly ThemeResolver themeResolver;
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
    MenuItem autostartItem;
    bool autoStartEnabled,autostartBusy,appSignalSent,lastAppReady;
    readonly AppStartupGate appStartup=new AppStartupGate();
    bool showFullProgress,showCompactProgress;
    bool showUltraProgress;
    string fullProgressMode,contourWindow;
    TextBlock compactStale;

    public OverlayWindow(string statePath):this(statePath,null) { }
    internal OverlayWindow(string statePath,ThemeResolver resolver) {
      stateFile=statePath;
      StartupCheckpoint("constructing");
      settingsFile=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(statePath)),"settings.json");
      var preferences=ReadPreferences();
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
      AllowsTransparency=true; Background=Brushes.Transparent;
      ShowInTaskbar=false; ShowActivated=false; Topmost=true; Focusable=false;
      FontFamily=new FontFamily("Segoe UI");
      panel=new Border { Width=Layout.PanelWidth,Height=Layout.PanelHeight,Background=Brush(palette.Surface),BorderBrush=Brush(palette.Border),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=new Thickness(3,5,3,5) };
      ToolTipService.SetIsEnabled(panel,false);
      var stack=new StackPanel(); panel.Child=stack;
      for(int i=0;i<2;i++) {
        if(i==1) {divider=new Border { Height=1,Background=Brush(palette.Divider),Margin=new Thickness(1,5,1,5) };stack.Children.Add(divider);}
        periods[i]=Text(i==0?"5 ч":"Нед.",10,FontWeights.Normal,palette.Secondary);
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
        if(menuDismiss!=null)menuDismiss.Dispose();tracker.Stop();
        if(eventHook!=IntPtr.Zero)Native.UnhookWinEvent(eventHook);
        if(stopWait!=null)stopWait.Unregister(null);if(stopEvent!=null)stopEvent.Dispose();
        tray.Visible=false;tray.Dispose();Command("exit");
      };
      stopEvent=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\ChatGPTQuotaOverlay.Stop");stopEvent.Reset();
      stopWait=ThreadPool.RegisterWaitForSingleObject(stopEvent,delegate {if(!closing)Dispatcher.BeginInvoke(new Action(Close));},null,-1,true);
      tracker.Interval=TimeSpan.FromMilliseconds(200); tracker.Tick+=delegate { UpdatePlacement(); };tracker.Start();
      nativeEvent=delegate(IntPtr hook,uint ev,IntPtr window,int obj,int child,uint thread,uint time) {
        if(!closing && (ev==3 || window==target && obj==0 && (ev==0x800B || ev==0x16 || ev==0x17))) Dispatcher.BeginInvoke(new Action(UpdatePlacement));
      };
      eventHook=Native.SetWinEventHook(3,0x800B,IntPtr.Zero,nativeEvent,0,0,2);
      StartupCheckpoint("constructed");
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
    bool RefreshTheme(bool force) {
      if(!force&&DateTime.UtcNow<nextThemeCheck)return false;
      nextThemeCheck=DateTime.UtcNow.AddSeconds(1);
      string resolved=themeResolver.Resolve(themeMode);
      if(resolved==effectiveTheme)return false;
      effectiveTheme=resolved;palette=resolved=="light"?ThemePalette.Light:ThemePalette.Dark;
      panel.Background=Brush(palette.Surface);panel.BorderBrush=Brush(palette.Border);divider.Background=Brush(palette.Divider);
      compactStale.Foreground=Brush(palette.Secondary);
      for(int i=0;i<2;i++) {
        periods[i].Foreground=Brush(palette.Secondary);markers[i].Foreground=Brush(palette.Secondary);
        values[i].Foreground=Brush(palette.Tone(rowTones[i]));times[i].Foreground=values[i].Foreground;
        dates[i].Foreground=i==1&&compact?values[i].Foreground:Brush(palette.Date);
        progressTracks[i].Background=Brush(palette.Track);
      }
      ApplyMenuPalette();ApplyTrayPalette();UpdateProgress();SavePreview();lastState="";return true;
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
      var refresh=new MenuItem {Header="Обновить лимиты"};refresh.Click+=delegate {Command("refresh");};optionsMenu.Items.Add(refresh);
      var modes=new MenuItem {Header="Вид панели"};
      fullModeItem=new MenuItem {Header="Полный",IsCheckable=true};fullModeItem.Click+=delegate {SelectMode("full");};
      compactModeItem=new MenuItem {Header="Компактный",IsCheckable=true};compactModeItem.Click+=delegate {SelectMode("compact");};
      ultraModeItem=new MenuItem {Header="Ультракомпактный",IsCheckable=true};ultraModeItem.Click+=delegate {SelectMode("ultra");};
      modes.Items.Add(fullModeItem);modes.Items.Add(compactModeItem);modes.Items.Add(ultraModeItem);optionsMenu.Items.Add(modes);
      var themes=new MenuItem {Header="Тема"};
      themeDarkItem=new MenuItem {Header="Тёмная",IsCheckable=true};themeDarkItem.Click+=delegate {SelectTheme("dark");};
      themeLightItem=new MenuItem {Header="Светлая",IsCheckable=true};themeLightItem.Click+=delegate {SelectTheme("light");};
      themeAutoItem=new MenuItem {Header="Авто",IsCheckable=true};themeAutoItem.Click+=delegate {SelectTheme("auto");};
      themes.Items.Add(themeDarkItem);themes.Items.Add(themeLightItem);themes.Items.Add(themeAutoItem);optionsMenu.Items.Add(themes);
      autostartItem=new MenuItem {Header="Автозапуск с ChatGPT",IsCheckable=true};autostartItem.Click+=delegate {SetAutostart(autostartItem.IsChecked);};optionsMenu.Items.Add(autostartItem);
      optionsMenu.Items.Add(new Separator {Style=separatorStyle});
      var fullSettings=new MenuItem {Header="Индикатор"};
      fullProgressItem=new MenuItem {Header="Полоски",IsCheckable=true};fullProgressItem.Click+=delegate {SelectFullProgress("bars");};
      fullOutlineItem=new MenuItem {Header="Контур",IsCheckable=true};fullOutlineItem.Click+=delegate {SelectFullProgress("outline");};
      fullBothItem=new MenuItem {Header="Полоски и контур",IsCheckable=true};fullBothItem.Click+=delegate {SelectFullProgress("both");};
      fullOffItem=new MenuItem {Header="Без индикатора",IsCheckable=true};fullOffItem.Click+=delegate {SelectFullProgress("off");};
      fullSettings.Items.Add(fullProgressItem);fullSettings.Items.Add(fullOutlineItem);fullSettings.Items.Add(fullBothItem);fullSettings.Items.Add(fullOffItem);
      optionsMenu.Items.Add(fullSettings);
      contourSourceItem=new MenuItem {Header="Лимит контура"};
      shortProgressItem=new MenuItem {Header="5 часов",IsCheckable=true};weeklyProgressItem=new MenuItem {Header="Неделя",IsCheckable=true};
      shortProgressItem.Click+=delegate {contourWindow="short";SavePreferences();UpdateProgress();SavePreview();};
      weeklyProgressItem.Click+=delegate {contourWindow="weekly";SavePreferences();UpdateProgress();SavePreview();};
      contourSourceItem.Items.Add(shortProgressItem);contourSourceItem.Items.Add(weeklyProgressItem);optionsMenu.Items.Add(contourSourceItem);
      optionsMenu.Items.Add(new Separator {Style=separatorStyle});
      pauseUpdatesItem=new MenuItem {Header="Приостановить обновление"};pauseUpdatesItem.Click+=delegate {TogglePaused();};optionsMenu.Items.Add(pauseUpdatesItem);
      var stop=new MenuItem {Header="Остановить панель"};stop.Click+=delegate {Close();};optionsMenu.Items.Add(stop);
      menuDismiss=new MenuDismissWatcher(Dispatcher,delegate {return !closing&&optionsMenu.IsOpen;},delegate {optionsMenu.IsOpen=false;},null);
      optionsMenu.Opened+=delegate {SyncMenu();menuDismiss.Start();};
      optionsMenu.Closed+=delegate {menuDismiss.Stop();};panel.ContextMenu=optionsMenu;
    }
    void SyncMenu() {
      fullProgressItem.IsChecked=fullProgressMode=="bars";fullOutlineItem.IsChecked=fullProgressMode=="outline";fullOffItem.IsChecked=fullProgressMode=="off";
      fullBothItem.IsChecked=fullProgressMode=="both";
      shortProgressItem.IsChecked=contourWindow=="short";weeklyProgressItem.IsChecked=contourWindow=="weekly";
      fullModeItem.IsChecked=preferredMode=="full";compactModeItem.IsChecked=preferredMode=="compact";ultraModeItem.IsChecked=preferredMode=="ultra";
      themeDarkItem.IsChecked=themeMode=="dark";themeLightItem.IsChecked=themeMode=="light";themeAutoItem.IsChecked=themeMode=="auto";
      autostartItem.IsChecked=autoStartEnabled;autostartItem.IsEnabled=!autostartBusy;
      pauseUpdatesItem.Header=paused?"Продолжить обновление":"Приостановить обновление";
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
          if(!succeeded&&tray.Visible)tray.ShowBalloonTip(4000,"Quota Panel","Не удалось изменить автозапуск. Проверь задачу панели в Планировщике Windows.",Forms.ToolTipIcon.Warning);
        }));
      });
    }
    void TogglePaused() {
      paused=!paused;pause.Text=paused?"Продолжить":"Приостановить";Command(paused?"pause":"resume");SyncMenu();UpdatePlacement();
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
      compactStale.Text=markers[0].Text.Length>0 || markers[1].Text.Length>0 ? "устар." : "";
      compactStale.Visibility=presentationMode=="compact" && compactStale.Text.Length>0?Visibility.Visible:Visibility.Collapsed;
    }
    static void Command(string command) { try { Console.Out.WriteLine("{\"command\":\""+command+"\"}");Console.Out.Flush(); } catch {} }
    void SetupTray() {
      tray.Icon=System.Drawing.SystemIcons.Information;
      tray.Text="Лимиты Codex / Work";
      var menu=new Forms.ContextMenuStrip();
      menu.Items.Add("Обновить сейчас",null,delegate { Command("refresh"); });
      pause.Click+=delegate {TogglePaused();};
      menu.Items.Add(pause);
      menu.Items.Add(new Forms.ToolStripSeparator());
      menu.Items.Add("Закрыть монитор",null,delegate { Close(); });
      tray.ContextMenuStrip=menu;ApplyTrayPalette();tray.Visible=true;
      tray.DoubleClick+=delegate {Command("refresh");};
    }
    void StartInput() {
      var thread=new Thread(delegate() {
        try { string line;while((line=Console.ReadLine())!=null) {
          var message=new JavaScriptSerializer().DeserializeObject(line) as Dictionary<string,object>;
          if(message!=null)Dispatcher.BeginInvoke(new Action(delegate { Apply(message); }));
        }} catch {} finally { if(!closing)Dispatcher.BeginInvoke(new Action(Close)); }
      });thread.IsBackground=true;thread.Start();Command("ready");
    }
    void Apply(Dictionary<string,object> message) {
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
      UpdateCompactStatus();UpdateProgress();
      SavePreview();lastState="";UpdatePlacement();
      if(receivedUpdates<=30)StartupCheckpoint("received-data");
    }
    static string Get(Dictionary<string,object> row,string key) { return row.ContainsKey(key)?Convert.ToString(row[key]):""; }
    bool IsTarget(IntPtr window) {
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
    void UpdatePlacement() {
      if(closing||hwnd==IntPtr.Zero)return;
      RefreshTheme(false);
      IntPtr foreground=Native.GetForegroundWindow();
      IntPtr root=Native.GetAncestor(foreground,3);
      if(IsTarget(root))target=root;
      else if(target==IntPtr.Zero||!Native.IsWindow(target)) {
        Native.EnumWindows(delegate(IntPtr w,IntPtr p){ if(IsTarget(w)) {var title=new StringBuilder(256);Native.GetWindowText(w,title,256);if(title.Length>0){target=w;return false;}}return true;},IntPtr.Zero);
      }
      uint popupProcess=0;if(optionsMenu.IsOpen)Native.GetWindowThreadProcessId(foreground,out popupProcess);
      bool active=foreground==target||root==target||(optionsMenu.IsOpen && popupProcess==Process.GetCurrentProcess().Id);
      Native.Rect rect=new Native.Rect();var origin=new Native.Point();int cloaked=0;
      bool valid=target!=IntPtr.Zero && Native.IsWindow(target) && Native.IsWindowVisible(target) && !Native.IsIconic(target);
      if(valid) {Native.DwmGetWindowAttribute(target,14,out cloaked,4);valid=cloaked==0&&Native.GetClientRect(target,out rect)&&Native.ClientToScreen(target,ref origin);}
      bool appPresent=target!=IntPtr.Zero&&Native.IsWindow(target)&&Native.IsWindowVisible(target);
      if(appPresent){Native.DwmGetWindowAttribute(target,14,out cloaked,4);appPresent=cloaked==0;}
      bool appReady=appStartup.Ready(target,appPresent,(long)(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency));
      if(!appSignalSent||lastAppReady!=appReady){appSignalSent=true;lastAppReady=appReady;Command(appReady?"app-open":"app-closed");}
      double scale=valid?Native.GetDpiForWindow(target)/96.0:1;
      var placement=Layout.Place(origin.X,origin.Y,rect.Right-rect.Left,rect.Bottom-rect.Top,scale,preferredMode);
      bool modeChanged=presentationMode!=placement.Mode;
      if(modeChanged)SetMode(placement.Mode);
      bool show=valid&&active&&!paused&&placement.Visible&&appReady;
      if(show) {
        if(!IsVisible) Show();
        ApplyNativePlacement(hwnd,placement,optionsMenu.IsOpen);
      } else if(IsVisible)Hide();
      if(modeChanged) {UpdateLayout();SavePreview();}
      IntPtr owner=Native.GetWindowLongPtr(hwnd,-8);uint ownerProcess=0;if(owner!=IntPtr.Zero)Native.GetWindowThreadProcessId(owner,out ownerProcess);
      var state=new {appReady=appReady,appPresent=appPresent,startupDelayMs=AppStartupGate.DelayMilliseconds,autoStartEnabled=autoStartEnabled,visible=show,paused=paused,compact=compact,preferredCompact=preferCompact,mode=presentationMode,preferredMode=preferredMode,theme=new {mode=themeMode,effective=effectiveTheme,appPreference=themeResolver.AppPreference,source=themeResolver.Source},target=target.ToInt64(),foreground=foreground.ToInt64(),owner=owner.ToInt64(),ownerProcess=ownerProcess,process=Process.GetCurrentProcess().Id,dpi=scale*96,client=new {x=origin.X,y=origin.Y,width=rect.Right-rect.Left,height=rect.Bottom-rect.Top},panel=new {x=placement.X,y=placement.Y,width=placement.Width,height=placement.Height},noActivate=(Native.GetWindowLongPtr(hwnd,-20).ToInt64()&0x08000000)!=0,clickThrough=(Native.GetWindowLongPtr(hwnd,-20).ToInt64()&0x20)!=0,clickToToggle=true,hoverTooltipEnabled=ToolTipService.GetIsEnabled(panel),progress=new {indicatorMode=fullProgressMode,fullEnabled=showFullProgress,fullMode=fullProgressMode,fullWindow=contourWindow,compactEnabled=showCompactProgress,compactWindow=contourWindow,ultraEnabled=showUltraProgress,ultraWindow=contourWindow,shortRemaining=remainingQuota[0],weeklyRemaining=remainingQuota[1]},view=view};
      string encoded=json.Serialize(state);
      diagnosticState=state;
      if(encoded!=lastState){lastState=encoded;try{File.WriteAllText(stateFile,encoded,Encoding.UTF8);stateWriteError="";}catch(Exception error){stateWriteError=error.GetType().Name+":"+error.HResult.ToString();}}
    }
    internal static void ApplyNativePlacement(IntPtr handle,Placement placement,bool menuOpen) {
      uint flags=0x10|0x40;
      if(menuOpen)flags|=0x4; // SWP_NOZORDER: never raise our panel over an open menu.
      Native.SetWindowPos(handle,new IntPtr(-1),(int)Math.Round(placement.X),(int)Math.Round(placement.Y),(int)Math.Round(placement.Width),(int)Math.Round(placement.Height),flags);
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
