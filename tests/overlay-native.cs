using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using QuotaOverlay;

// This entry point deliberately replaces Program.Main at compile time. Most cases
// never show a window; the viewport case uses only its own offscreen no-activate
// window. No tray is installed; previews are written under the runner's temp dir.
class OverlayNativeTest {
  static readonly BindingFlags PrivateInstance=BindingFlags.NonPublic|BindingFlags.Instance;
  static OverlayWindow NewWindow(string statePath) {return new OverlayWindow(statePath,new ThemeResolver(Path.Combine(Path.GetDirectoryName(statePath),"test-app-config.toml"),delegate{return false;}));}
  static void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
  static T Field<T>(OverlayWindow window,string name) {
    var field=typeof(OverlayWindow).GetField(name,PrivateInstance);
    Check(field!=null,"native overlay must provide the "+name+" state for its presentation");
    return (T)field.GetValue(window);
  }
  static MethodInfo Method(string name) {
    var method=typeof(OverlayWindow).GetMethod(name,PrivateInstance);
    Check(method!=null,"native overlay must implement "+name+" for compact mode");return method;
  }
  static void Invoke(OverlayWindow window,string name,params object[] arguments) {
    try { Method(name).Invoke(window,arguments); }
    catch(TargetInvocationException error) { throw error.InnerException??error; }
  }
  static Dictionary<string,object> Row(string period,string value,string date,string time,string marker,string tone) {
    double numeric;object remaining=Double.TryParse(value.TrimEnd('%'),NumberStyles.Float,CultureInfo.InvariantCulture,out numeric)?(object)numeric:null;
    return new Dictionary<string,object> { {"period",period},{"value",value},{"date",date},{"time",time},{"marker",marker},{"tone",tone},{"remaining",remaining} };
  }
  static void Apply(OverlayWindow window,Dictionary<string,object> first,Dictionary<string,object> second) {
    Invoke(window,"Apply",new Dictionary<string,object> { {"rows",new object[] { first,second }} });
  }
  static void Known(OverlayWindow window) {
    Apply(window,Row("5 ч","100%","31.12","23:59","устар.","normal"),Row("Нед.","100%","31.12","00:00","устар.","normal"));
  }
  static void AssertUnshown(OverlayWindow window,bool allowOwnHandle=false) {
    Check(!window.IsVisible,"native tests must never show the overlay");
    Check(allowOwnHandle || Field<IntPtr>(window,"hwnd")==IntPtr.Zero,"native layout tests must never create the overlay HWND");
  }
  static Color ColorOf(Brush brush) { return ((SolidColorBrush)brush).Color; }
  static Color ParseColor(string value) { return (Color)ColorConverter.ConvertFromString(value); }
  static double Channel(byte component) {
    double value=component/255.0;return value<=0.04045?value/12.92:Math.Pow((value+0.055)/1.055,2.4);
  }
  static double Luminance(Color value) { return 0.2126*Channel(value.R)+0.7152*Channel(value.G)+0.0722*Channel(value.B); }
  static void AssertReadableColor(OverlayWindow window,int index,string expected) {
    Color background=ColorOf(Field<Border>(window,"panel").Background);
    foreach(string name in new[] { "values","times" }) {
      Color foreground=ColorOf(Field<TextBlock[]>(window,name)[index].Foreground);
      Check(foreground==ParseColor(expected),name+"["+index+"] must apply the model's muted tone "+expected);
      double contrast=(Math.Max(Luminance(background),Luminance(foreground))+0.05)/(Math.Min(Luminance(background),Luminance(foreground))+0.05);
      Check(contrast>=4.5,name+"["+index+"] contrast must be readable; actual "+contrast.ToString("F2",CultureInfo.InvariantCulture));
    }
  }
  static void LayoutTest(OverlayWindow window) {
    Invoke(window,"SetCompact",false);
    Known(window);
    var panel=Field<Border>(window,"panel");
    panel.Measure(new Size(Layout.PanelWidth,Layout.PanelHeight));
    panel.Arrange(new Rect(0,0,Layout.PanelWidth,Layout.PanelHeight));panel.UpdateLayout();
    double available=panel.ActualWidth-panel.Padding.Left-panel.Padding.Right-panel.BorderThickness.Left-panel.BorderThickness.Right;
    Check(available>0,"panel must leave positive content width");
    foreach(string name in new[] { "periods","values","dates","times","markers" }) {
      var labels=Field<TextBlock[]>(window,name);
      for(int i=0;i<labels.Length;i++) {
        var label=labels[i];
        Check(label.Text.Length>0,name+"["+i+"] must be populated");
        Check(label.ActualWidth>0 && label.ActualWidth<=available+0.01,name+" must use the available panel width");
        var text=new FormattedText(label.Text,CultureInfo.GetCultureInfo("ru-RU"),label.FlowDirection,
          new Typeface(label.FontFamily,label.FontStyle,label.FontWeight,label.FontStretch),label.FontSize,label.Foreground);
        Check(text.WidthIncludingTrailingWhitespace<=label.ActualWidth+0.01,
          name+"["+i+"] '"+label.Text+"' needs "+text.WidthIncludingTrailingWhitespace.ToString("F2",CultureInfo.InvariantCulture)+
          " DIP but has "+label.ActualWidth.ToString("F2",CultureInfo.InvariantCulture)+" DIP");
        Rect bounds=label.TransformToAncestor(panel).TransformBounds(new Rect(new Point(0,0),label.RenderSize));
        Check(bounds.Bottom<=panel.ActualHeight-panel.Padding.Bottom-panel.BorderThickness.Bottom+0.01,name+" must fit vertically");
      }
    }
    var times=Field<TextBlock[]>(window,"times");
    Check(times[0].FontWeight==FontWeights.Bold && times[1].FontWeight==FontWeights.Bold,"reset times must stay bold in both rows");
    AssertFullPresentation(window);
    Console.WriteLine("PASS native layout: 100%, dates, Cyrillic labels, stale markers and bold times fit "+available.ToString("F2",CultureInfo.InvariantCulture)+" DIP");
  }
  static void ToneTest(OverlayWindow window) {
    Invoke(window,"SetCompact",false);
    Apply(window,Row("5 ч","10%","23.10","19:55","","critical"),Row("Нед.","30%","29.10","21:10","","warning"));
    AssertReadableColor(window,0,"#E6A0A1");AssertReadableColor(window,1,"#E3B382");
    var dates=Field<TextBlock[]>(window,"dates");var periods=Field<TextBlock[]>(window,"periods");
    Check(dates[0].Text=="23.10" && dates[1].Text=="29.10","both reset dates must be populated");
    Check(periods[0].Text=="5 ч" && periods[1].Text=="Нед.","both period labels must be populated");
    Check(Field<TextBlock[]>(window,"times")[0].Text=="19:55" && Field<TextBlock[]>(window,"times")[1].Text=="21:10","both reset times must be populated");
    Apply(window,Row("5 ч","31%","23.10","19:55","","normal"),Row("Нед.","100%","29.10","21:10","","normal"));
    AssertReadableColor(window,0,"#ECECEE");AssertReadableColor(window,1,"#ECECEE");
    Console.WriteLine("PASS native tones: muted critical/warning/normal colours reach both values and times with contrast >= 4.5");
  }
  static void UnknownTest(OverlayWindow window) {
    Invoke(window,"SetCompact",false);
    foreach(string name in new[] { "values","dates","times" })
      foreach(var label in Field<TextBlock[]>(window,name))Check(label.Text=="—","initial "+name+" must show an unknown placeholder");
    Known(window);
    Apply(window,Row("5 ч","—","—","—","","unknown"),Row("Нед.","—","—","—","","unknown"));
    foreach(string name in new[] { "values","dates","times" })
      foreach(var label in Field<TextBlock[]>(window,name))Check(label.Text=="—",name+" must clear the previous known value when unknown");
    foreach(var label in Field<TextBlock[]>(window,"markers"))Check(label.Text=="","unknown must clear stale markers");
    AssertReadableColor(window,0,"#B1B2B5");AssertReadableColor(window,1,"#B1B2B5");
    Apply(window,new Dictionary<string,object> { {"period","5 ч"},{"tone","unknown"} },new Dictionary<string,object> { {"period","Нед."},{"tone","unknown"} });
    foreach(string name in new[] { "values","dates","times" })
      foreach(var label in Field<TextBlock[]>(window,name))Check(label.Text=="","missing "+name+" must not fabricate a quota or reset");
    Console.WriteLine("PASS native unknown: initial, transition and absent data never invent quota/reset values");
  }
  static void ProtocolTest(OverlayWindow window) {
    Invoke(window,"SetCompact",false);
    // Exercise the production line reader with redirected UTF-8 stdin without
    // calling the production entry point, mutex, Loaded handler, or tray setup.
    Console.SetIn(new StreamReader(Console.OpenStandardInput(),new UTF8Encoding(false,true)));
    var frame=new DispatcherFrame();bool closed=false,timedOut=false;
    window.Closed+=delegate {closed=true;frame.Continue=false;};
    var deadline=new DispatcherTimer { Interval=TimeSpan.FromSeconds(5) };
    deadline.Tick+=delegate {timedOut=true;frame.Continue=false;};deadline.Start();
    try { Invoke(window,"StartInput");Dispatcher.PushFrame(frame); }
    finally {deadline.Stop();}
    Check(!timedOut,"production line input must close promptly at EOF");
    Check(closed,"production line input must close its unshown window at EOF");
    var periods=Field<TextBlock[]>(window,"periods");var markers=Field<TextBlock[]>(window,"markers");
    Check(periods[0].Text=="5 ч" && periods[1].Text=="Нед.","UTF-8 NDJSON must preserve Cyrillic periods from its final line");
    Check(markers[0].Text=="устар." && markers[1].Text=="устар.","UTF-8 NDJSON must preserve Cyrillic stale markers");
    Check(Field<TextBlock[]>(window,"values")[0].Text=="100%" && Field<TextBlock[]>(window,"times")[1].Text=="23:59","line input must apply the second complete message");
    Console.WriteLine("PASS native protocol: multiple Unicode NDJSON lines apply and EOF closes the unshown window");
  }
  static void AssertFullPresentation(OverlayWindow window) {
    var panel=Field<Border>(window,"panel");
    Check(Math.Abs(panel.Height-Layout.PanelHeight)<0.01,"full mode must restore the height allocated for text and bars");
    Check(panel.Padding==new Thickness(3,5,3,5),"full mode must restore its original panel padding");
    foreach(string name in new[] { "periods","values","dates","times","markers" })
      foreach(var label in Field<TextBlock[]>(window,name))Check(label.Visibility==Visibility.Visible,"full mode must restore "+name);
    foreach(var label in Field<TextBlock[]>(window,"values"))Check(label.FontSize==16 && label.FontWeight==FontWeights.SemiBold,"full quota font must remain 16 DIP semibold");
    foreach(var label in Field<TextBlock[]>(window,"dates"))Check(label.FontSize==10.5 && label.FontWeight==FontWeights.Normal,"full mode must restore both original date fonts");
    foreach(var label in Field<TextBlock[]>(window,"times"))Check(label.FontSize==11.5 && label.FontWeight==FontWeights.Bold,"full mode must restore both bold time fonts");
    Check(Field<TextBlock>(window,"compactStale").Visibility==Visibility.Collapsed,"full mode must use row stale markers");
  }
  static void AssertCompactPresentation(OverlayWindow window) {
    var panel=Field<Border>(window,"panel");
    Check(Math.Abs(panel.Height-100)<0.01,"compact mode must use a 100 DIP panel");
    bool bars=Field<Border[]>(window,"progressTracks")[0].Visibility==Visibility.Visible;
    Check(panel.Padding==new Thickness(3,bars?2:3,3,bars?2:3),"compact padding must fit the selected graphics without shrinking text");
    foreach(var label in Field<TextBlock[]>(window,"periods"))Check(label.Visibility==Visibility.Collapsed,"compact mode must hide both period labels");
    foreach(var label in Field<TextBlock[]>(window,"markers"))Check(label.Visibility==Visibility.Collapsed,"compact mode must hide the duplicate row stale markers");
    Check(Field<TextBlock[]>(window,"dates")[0].Visibility==Visibility.Collapsed,"compact mode must hide the five-hour reset date");
    Check(Field<TextBlock[]>(window,"times")[1].Visibility==Visibility.Collapsed,"compact mode must hide the weekly reset time");
    foreach(var label in Field<TextBlock[]>(window,"values"))Check(label.Visibility==Visibility.Visible && label.FontSize==16 && label.FontWeight==FontWeights.SemiBold,"compact quota values must keep their full 16 DIP semibold font");
    var time=Field<TextBlock[]>(window,"times")[0];var date=Field<TextBlock[]>(window,"dates")[1];
    Check(time.Visibility==Visibility.Visible && time.FontSize==11.5 && time.FontWeight==FontWeights.Bold,"compact top reset time must remain 11.5 DIP bold");
    Check(date.Visibility==Visibility.Visible && date.FontSize==11.5 && date.FontWeight==FontWeights.Bold,"compact weekly reset date must be 11.5 DIP bold");
    var stack=panel.Child as StackPanel;Check(stack!=null,"panel must expose its real arranged content");
    Border divider=Field<Border>(window,"divider");
    Check(divider!=null && divider.Margin.Top==(bars?1:3) && divider.Margin.Bottom==(bars?1:3),"compact divider spacing must fit the selected graphics");
  }
  static void AssertCompactFits(OverlayWindow window) {
    var panel=Field<Border>(window,"panel");
    panel.Measure(new Size(46,100));panel.Arrange(new Rect(0,0,46,100));panel.UpdateLayout();
    var visible=new List<TextBlock> {Field<TextBlock[]>(window,"values")[0],Field<TextBlock[]>(window,"times")[0],Field<TextBlock[]>(window,"values")[1],Field<TextBlock[]>(window,"dates")[1]};
    var stale=Field<TextBlock>(window,"compactStale");if(stale.Visibility==Visibility.Visible)visible.Add(stale);
    double left=panel.Padding.Left+panel.BorderThickness.Left,right=46-panel.Padding.Right-panel.BorderThickness.Right;
    double top=panel.Padding.Top+panel.BorderThickness.Top,bottom=100-panel.Padding.Bottom-panel.BorderThickness.Bottom;
    foreach(var label in visible) {
      Check(label.ActualWidth>0 && label.ActualHeight>0,"each compact label must occupy real layout space");
      Rect bounds=label.TransformToAncestor(panel).TransformBounds(new Rect(new Point(0,0),label.RenderSize));
      Check(bounds.Left>=left-0.01 && bounds.Right<=right+0.01 && bounds.Top>=top-0.01 && bounds.Bottom<=bottom+0.01,
        "compact label '"+label.Text+"' must fit actual padded 46 x 100 DIP content bounds; actual "+bounds);
      var text=new FormattedText(label.Text,CultureInfo.GetCultureInfo("ru-RU"),label.FlowDirection,new Typeface(label.FontFamily,label.FontStyle,label.FontWeight,label.FontStretch),label.FontSize,label.Foreground);
      Check(text.WidthIncludingTrailingWhitespace<=label.ActualWidth+0.01,"compact label '"+label.Text+"' must fit at its unchanged font size");
    }
    double previousBottom=top;
    for(int i=0;i<4;i++) {
      Rect bounds=visible[i].TransformToAncestor(panel).TransformBounds(new Rect(new Point(0,0),visible[i].RenderSize));
      Check(bounds.Top>=previousBottom-0.01,"compact values and reset labels must stay in top-to-bottom order without overlap");previousBottom=bounds.Bottom;
    }
  }
  static void CompactTest(OverlayWindow window) {
    Check(Field<bool>(window,"preferCompact"),"missing settings must default to compact mode");
    Known(window);AssertCompactPresentation(window);AssertCompactFits(window);
    var values=Field<TextBlock[]>(window,"values");var times=Field<TextBlock[]>(window,"times");var dates=Field<TextBlock[]>(window,"dates");
    Check(values[0].Text=="100%" && times[0].Text=="23:59","compact top must show the five-hour value and reset time");
    Check(values[1].Text=="100%" && dates[1].Text=="31.12","compact bottom must show the weekly value and reset date");
    var stale=Field<TextBlock>(window,"compactStale");
    Check(stale.Visibility==Visibility.Visible && stale.Text.IndexOf("устар",StringComparison.OrdinalIgnoreCase)>=0,"compact stale data must show one explicit shared stale label");
    Apply(window,Row("5 ч","10%","23.10","19:55","","critical"),Row("Нед.","30%","29.10","21:10","устар.","warning"));
    AssertReadableColor(window,0,"#E6A0A1");AssertReadableColor(window,1,"#E3B382");
    Check(ColorOf(dates[1].Foreground)==ParseColor("#E3B382"),"visible compact weekly reset date must preserve its warning colour");
    Check(stale.Visibility==Visibility.Visible,"weekly-only stale data must keep the shared compact indication");
    Apply(window,Row("5 ч","10%","23.10","19:55","устар.","critical"),Row("Нед.","30%","29.10","21:10","","warning"));
    Check(stale.Visibility==Visibility.Visible,"five-hour-only stale data must keep the shared compact indication");
    Apply(window,Row("5 ч","—","—","—","","unknown"),Row("Нед.","—","—","—","","unknown"));
    Check(values[0].Text=="—" && values[1].Text=="—" && times[0].Text=="—" && dates[1].Text=="—","compact unknown data must clear the previous quota and reset");
    Check(stale.Visibility==Visibility.Collapsed,"unknown current data must remove an earlier compact stale indication");
    AssertReadableColor(window,0,"#B1B2B5");AssertReadableColor(window,1,"#B1B2B5");
    AssertCompactPresentation(window);AssertCompactFits(window);
    Console.WriteLine("PASS native compact: 100% values, top reset time, weekly reset date and shared stale status fit real 46 x 100 DIP bounds");
  }
  static bool SavedCompact(string directory) {
    string file=Path.Combine(directory,"settings.json");Check(File.Exists(file),"click must persist settings.json beside the state file");
    var settings=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(file)) as Dictionary<string,object>;
    Check(settings!=null && settings.ContainsKey("compact") && settings["compact"] is bool,"settings must store compact as a JSON boolean");return (bool)settings["compact"];
  }
  static void ClickPanel(OverlayWindow window) {
    var click=new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left) { RoutedEvent=UIElement.MouseLeftButtonUpEvent };
    Field<Border>(window,"panel").RaiseEvent(click);AssertUnshown(window);
  }
  static void ToggleTest(ref OverlayWindow window,string directory) {
    Method("ToggleMode");Known(window);AssertCompactPresentation(window);
    ClickPanel(window);Check(Field<string>(window,"presentationMode")=="ultra" && SavedCompact(directory),"first click from compact must select and persist ultra");
    ClickPanel(window);Check(!Field<bool>(window,"preferCompact") && !SavedCompact(directory),"next panel click must prefer full mode and persist false");AssertFullPresentation(window);
    // Close each constructor's hook/timer/stop wait before opening the next instance.
    window.Close();window=NewWindow(Path.Combine(directory,"state.json"));AssertUnshown(window);
    Check(!Field<bool>(window,"preferCompact"),"a reconstructed window must restore the user's full preference");AssertFullPresentation(window);
    Known(window);ClickPanel(window);Check(Field<bool>(window,"preferCompact") && SavedCompact(directory),"second panel click must prefer compact mode and persist true");AssertCompactPresentation(window);AssertCompactFits(window);
    window.Close();window=NewWindow(Path.Combine(directory,"state.json"));AssertUnshown(window);
    Check(Field<bool>(window,"preferCompact"),"a reconstructed window must restore the user's compact preference");Known(window);AssertCompactPresentation(window);
    Console.WriteLine("PASS native toggle: routed panel clicks preserve full and compact preferences across window reconstruction");
  }
  static void SettingsTest(ref OverlayWindow window,string directory) {
    string[] fixtures={"{broken", "{}", "{\"compact\":\"false\"}", "{\"compact\":null}", "null"};
    foreach(string fixture in fixtures) {
      window.Close();File.WriteAllText(Path.Combine(directory,"settings.json"),fixture);
      window=NewWindow(Path.Combine(directory,"state.json"));AssertUnshown(window);
      Check(Field<bool>(window,"preferCompact"),"invalid or missing compact setting must safely default to compact: "+fixture);Known(window);AssertCompactPresentation(window);
    }
    Console.WriteLine("PASS native settings: malformed JSON, absent compact, string, null field and null document default to compact");
  }
  static void InputTest(OverlayWindow window) {
    var method=Method("WindowProc");
    object[] hit={IntPtr.Zero,0x84,IntPtr.Zero,IntPtr.Zero,false};var result=(IntPtr)method.Invoke(window,hit);
    Check((bool)hit[4] && result==new IntPtr(1),"overlay must return HTCLIENT so its own panel receives clicks");
    object[] activate={IntPtr.Zero,0x21,IntPtr.Zero,IntPtr.Zero,false};result=(IntPtr)method.Invoke(window,activate);
    Check((bool)activate[4] && result==new IntPtr(3),"panel clicks must return MA_NOACTIVATE and preserve the foreground application");
    Known(window);
    Console.WriteLine("PASS native input: panel hit testing accepts its own clicks without activating the overlay");
  }
  static void StylesTest(OverlayWindow window) {
    // Create only our own hidden HWND: never enumerate, read or send input to an external window.
    IntPtr handle=new WindowInteropHelper(window).EnsureHandle();AssertUnshown(window,true);
    Check(handle!=IntPtr.Zero && Field<IntPtr>(window,"hwnd")==handle,"source initialization must attach to the overlay's own hidden HWND");
    long style=Native.GetWindowLongPtr(handle,-20).ToInt64();
    Check((style&0x08000000L)!=0,"overlay must retain WS_EX_NOACTIVATE");
    Check((style&0x00000080L)!=0,"overlay must remain a tool window");
    Check((style&0x00000020L)==0,"overlay must remove WS_EX_TRANSPARENT so real native clicks can reach its panel");
    Check(!window.ShowActivated && !window.ShowInTaskbar,"clickable overlay must preserve nonactivating taskbar-free window flags");
    Invoke(window,"SavePreview");
    Console.WriteLine("PASS native styles: own hidden HWND accepts clicks while retaining nonactivating tool-window styles");
  }
  static void TooltipTest(OverlayWindow window) {
    Known(window);
    var panel=Field<Border>(window,"panel");
    Check(panel.ToolTip==null,"panel hover must never show a redundant tooltip");
    Check(!ToolTipService.GetIsEnabled(panel),"panel hover tooltips must stay disabled");
    Invoke(window,"ToggleMode");Known(window);
    Check(panel.ToolTip==null && !ToolTipService.GetIsEnabled(panel),"switching full/compact or receiving data must not recreate the tooltip");
    Console.WriteLine("PASS native tooltip: no panel hover popup in either mode or after quota refresh");
  }
  static void MenuClick(MenuItem item) {
    typeof(MenuItem).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(item,null);
    PumpDispatcher();
  }
  static void PumpDispatcher() {
    var frame=new DispatcherFrame();
    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate {frame.Continue=false;}));
    Dispatcher.PushFrame(frame);
  }
  static void ProgressTest(OverlayWindow window) {
    Invoke(window,"SetCompact",false);
    Apply(window,Row("5 ч","30%","25.10","06:13","","warning"),Row("Нед.","10%","31.10","20:10","","critical"));
    var tracks=Field<Border[]>(window,"progressTracks");var fills=Field<Border[]>(window,"progressFills");
    Check(tracks[0].Visibility==Visibility.Visible && tracks[1].Visibility==Visibility.Visible,"full mode must show two bars");
    Check(Math.Abs(fills[0].Width/tracks[0].Width-.30)<.001 && Math.Abs(fills[1].Width/tracks[1].Width-.10)<.001,"bars must represent each remaining fraction");
    Check(ColorOf(fills[0].Background)==ParseColor("#E3B382") && ColorOf(fills[1].Background)==ParseColor("#E6A0A1"),"each bar must apply its own threshold colour");
    var panel=Field<Border>(window,"panel");panel.Measure(new Size(Layout.PanelWidth,Layout.PanelHeight));panel.Arrange(new Rect(0,0,Layout.PanelWidth,Layout.PanelHeight));panel.UpdateLayout();
    for(int i=0;i<2;i++) {
      Rect bar=tracks[i].TransformToAncestor(panel).TransformBounds(new Rect(new Point(0,0),tracks[i].RenderSize));
      Rect time=Field<TextBlock[]>(window,"times")[i].TransformToAncestor(panel).TransformBounds(new Rect(new Point(0,0),Field<TextBlock[]>(window,"times")[i].RenderSize));
      Check(bar.Top>=time.Bottom && bar.Bottom<=panel.ActualHeight-5,"bars must fit below the reset times");
    }
    MenuClick(Field<MenuItem>(window,"fullOutlineItem"));Invoke(window,"SetCompact",true);
    var outline=Field<System.Windows.Shapes.Path>(window,"progressOutline");
    Check(outline.Visibility==Visibility.Visible && tracks[0].Visibility==Visibility.Collapsed && tracks[1].Visibility==Visibility.Collapsed,"compact uses only the frame indicator");
    Check(ColorOf(outline.Stroke)==ParseColor("#E3B382"),"default outline must use five-hour quota");
    MenuClick(Field<MenuItem>(window,"weeklyProgressItem"));
    Check(Field<string>(window,"contourWindow")=="weekly" && ColorOf(outline.Stroke)==ParseColor("#E6A0A1"),"weekly selection must change the outline data and colour");
    Apply(window,Row("5 ч","100%","25.10","06:13","","normal"),Row("Нед.","0%","31.10","20:10","","critical"));
    Check(!outline.Data.IsEmpty() && Math.Abs(outline.Data.Bounds.Bottom-98.75)<.1 && ColorOf(outline.Stroke)==ParseColor("#E6A0A1"),"exhausted weekly quota must show a complete red outline");
    Apply(window,Row("5 ч","—","—","—","","unknown"),Row("Нед.","—","—","—","","unknown"));
    Check(outline.Data.IsEmpty() && fills[0].Width==0 && fills[1].Width==0,"unknown quota cannot appear as full progress");
    Known(window);Check(outline.Opacity<1,"stale known quota must dim the outline");
    Console.WriteLine("PASS native progress: remaining bars, selected contour, thresholds, unknown and stale state");
  }
  static void ProgressSettingsTest(ref OverlayWindow window,string directory) {
    File.WriteAllText(Path.Combine(directory,"settings.json"),"{\"compact\":true,\"externalPreference\":\"keep\"}");
    Check(Field<Border>(window,"panel").ContextMenu!=null,"panel must expose its settings by right click");
    MenuClick(Field<MenuItem>(window,"fullOffItem"));
    Check(!Field<bool>(window,"showFullProgress") && !Field<bool>(window,"showCompactProgress") && !Field<bool>(window,"showUltraProgress"),"shared off must disable every contour");
    MenuClick(Field<MenuItem>(window,"weeklyProgressItem"));
    ClickPanel(window);
    window.Close();window=NewWindow(Path.Combine(directory,"state.json"));AssertUnshown(window);
    Check(!Field<bool>(window,"showFullProgress") && !Field<bool>(window,"showCompactProgress") && Field<string>(window,"contourWindow")=="weekly","all three settings must survive mode changes and reconstruction");
    var saved=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(directory,"settings.json"))) as Dictionary<string,object>;
    Check(Convert.ToString(saved["externalPreference"])=="keep","indicator settings must preserve other stored preferences");
    Check(Convert.ToString(saved["indicatorMode"])=="off","the shared mode must be stored explicitly");
    MenuClick(Field<MenuItem>(window,"fullBothItem"));MenuClick(Field<MenuItem>(window,"weeklyProgressItem"));
    Check(Field<MenuItem>(window,"weeklyProgressItem").IsChecked && !Field<MenuItem>(window,"shortProgressItem").IsChecked,"selected radio option cannot turn itself off");
    Check(Field<bool>(window,"showFullProgress") && Field<bool>(window,"showCompactProgress") && Field<bool>(window,"showUltraProgress"),"shared both must enable every contour");
    Known(window);Console.WriteLine("PASS native progress-settings: shared indicator persistence and exclusive shared contour source");
  }
  static void ContourTest(OverlayWindow window) {
    var type=typeof(OverlayWindow).Assembly.GetType("QuotaOverlay.ProgressGeometry");Check(type!=null,"rounded contour geometry must exist");
    var create=type.GetMethod("Create",BindingFlags.Public|BindingFlags.Static);
    foreach(double fraction in new[]{.25,.5,.75,1.0}) {
      var geometry=(PathGeometry)create.Invoke(null,new object[]{46.0,100.0,fraction});Point start,tangent,end;
      geometry.GetPointAtFractionLength(0,out start,out tangent);geometry.GetPointAtFractionLength(1,out end,out tangent);
      Check(Math.Abs(start.X-23)<.1 && Math.Abs(start.Y-1.25)<.1,"contour must start at top centre");
      Point expected=fraction==.25?new Point(44.75,50):fraction==.5?new Point(23,98.75):fraction==.75?new Point(1.25,50):new Point(23,1.25);
      Check((end-expected).Length<.15,"clockwise fraction endpoint must match right/bottom/left/top: "+fraction+" actual "+end);
      Check(geometry.Bounds.Left>=1.2 && geometry.Bounds.Top>=1.2 && geometry.Bounds.Right<=44.8 && geometry.Bounds.Bottom<=98.8,"contour must remain within the rounded frame");
    }
    Check(((Geometry)create.Invoke(null,new object[]{46.0,100.0,0.0})).IsEmpty(),"zero geometric length must draw no segment");
    Check(((Geometry)create.Invoke(null,new object[]{46.0,100.0,Double.NaN})).IsEmpty(),"unknown quota must draw no contour");
    Known(window);Console.WriteLine("PASS native contour: clockwise length follows the actual rounded perimeter at all quadrants");
  }
  static void MenuActionsTest(OverlayWindow window) {
    Known(window);var menu=Field<ContextMenu>(window,"optionsMenu");
    foreach(string header in new[]{"Обновить лимиты","Приостановить обновление","Продолжить обновление","Остановить панель"}) {
      MenuItem found=null;foreach(object item in menu.Items) {
        var candidate=item as MenuItem;if(candidate!=null && Convert.ToString(candidate.Header)==header){found=candidate;break;}
      }
      Check(found!=null,"right-click menu must expose "+header);MenuClick(found);
    }
    Check(!Field<bool>(window,"paused") && Field<bool>(window,"closing"),"menu actions must resume updates and cooperatively close only this window");
    Console.WriteLine("PASS native menu-actions: real menu clicks send refresh, pause, resume and cooperative stop");
  }
  static void FullIndicatorTest(ref OverlayWindow window,string directory) {
    Check(Field<string>(window,"fullProgressMode")=="bars","full indicator must default to existing bars");
    var fullSource=Field<MenuItem>(window,"contourSourceItem");
    Check(fullSource.IsEnabled && Field<ContextMenu>(window,"optionsMenu").Items.Contains(fullSource),"full contour source must be an explicit top-level selection available even with bars only");
    Apply(window,Row("5 ч","30%","25.10","06:13","","warning"),Row("Нед.","10%","31.10","20:10","","critical"));
    MenuClick(Field<MenuItem>(window,"fullOutlineItem"));MenuClick(Field<MenuItem>(window,"weeklyProgressItem"));
    Invoke(window,"SetCompact",false);
    var outline=Field<System.Windows.Shapes.Path>(window,"progressOutline");
    var tracks=Field<Border[]>(window,"progressTracks");
    Check(outline.Visibility==Visibility.Visible && Math.Abs(outline.Height-Layout.PanelHeight)<.01,"full outline must follow the full-height frame");
    Check(tracks[0].Visibility==Visibility.Collapsed && tracks[1].Visibility==Visibility.Collapsed,"full outline must replace both bars");
    Check(ColorOf(outline.Stroke)==ParseColor("#E6A0A1"),"full weekly outline must use weekly quota and threshold");
    Apply(window,Row("5 ч","30%","25.10","06:13","","warning"),Row("Нед.","100%","31.10","20:10","","normal"));
    Check(Math.Abs(outline.Data.Bounds.Bottom-(Layout.PanelHeight-1.25))<.1,"full contour geometry must reach the bottom of the tall frame");
    Apply(window,Row("5 ч","30%","25.10","06:13","","warning"),Row("Нед.","10%","31.10","20:10","","critical"));
    Check(Field<string>(window,"contourWindow")=="weekly" && Field<bool>(window,"showCompactProgress"),"shared contour source must not change the compact enable flag");
    Invoke(window,"SetCompact",true);
    Check(Math.Abs(outline.Height-100)<.01 && ColorOf(outline.Stroke)==ParseColor("#E6A0A1"),"compact must keep the shared weekly source with compact geometry");
    Invoke(window,"SetCompact",false);MenuClick(Field<MenuItem>(window,"fullOffItem"));
    Check(outline.Visibility==Visibility.Collapsed && tracks[0].Visibility==Visibility.Collapsed,"off must hide all full-mode indicators");
    MenuClick(Field<MenuItem>(window,"fullProgressItem"));MenuClick(Field<MenuItem>(window,"fullProgressItem"));
    Check(Field<MenuItem>(window,"fullProgressItem").IsChecked && !Field<MenuItem>(window,"fullOutlineItem").IsChecked && !Field<MenuItem>(window,"fullOffItem").IsChecked,"full choice must remain exclusive, including a repeated click");
    Check(tracks[0].Visibility==Visibility.Visible && outline.Visibility==Visibility.Collapsed,"bars choice must restore both bars");
    MenuClick(Field<MenuItem>(window,"fullOutlineItem"));
    window.Close();window=NewWindow(Path.Combine(directory,"state.json"));AssertUnshown(window);
    Check(Field<string>(window,"fullProgressMode")=="outline" && Field<string>(window,"contourWindow")=="weekly","full mode and source must survive reconstruction");
    Check(Field<string>(window,"contourWindow")=="weekly","shared source must persist across modes");
    Known(window);Console.WriteLine("PASS native full-indicator: bars, off, full-size selectable contour and independent saved settings");
  }
  static void FullMigrationTest(ref OverlayWindow window,string directory) {
    window.Close();File.WriteAllText(Path.Combine(directory,"settings.json"),"{\"showFullProgress\":false,\"showCompactProgress\":false,\"compactProgressWindow\":\"weekly\"}");
    window=NewWindow(Path.Combine(directory,"state.json"));AssertUnshown(window);
    Check(Field<string>(window,"fullProgressMode")=="off" && !Field<bool>(window,"showCompactProgress"),"existing disabled preferences must stay disabled during migration");
    Check(Field<string>(window,"contourWindow")=="weekly","migration must preserve compact source");
    window.Close();File.WriteAllText(Path.Combine(directory,"settings.json"),"{\"fullProgressMode\":\"bad\",\"fullProgressWindow\":\"year\",\"showFullProgress\":true}");
    window=NewWindow(Path.Combine(directory,"state.json"));AssertUnshown(window);
    Check(Field<string>(window,"fullProgressMode")=="bars" && Field<string>(window,"contourWindow")=="short","invalid full preferences must fall back safely");
    window.Close();File.WriteAllText(Path.Combine(directory,"settings.json"),"{\"indicatorMode\":\"both\",\"fullProgressMode\":\"off\",\"showFullProgress\":false,\"showCompactProgress\":false}");
    window=NewWindow(Path.Combine(directory,"state.json"));AssertUnshown(window);
    Check(Field<string>(window,"fullProgressMode")=="both" && Field<bool>(window,"showCompactProgress") && Field<bool>(window,"showUltraProgress"),"valid shared mode must take precedence over old per-view fields");
    Known(window);Console.WriteLine("PASS native full-migration: legacy enable flags, safe fallback and shared preference precedence");
  }
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateWindowEx(int extended,string className,string name,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr data);
  [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr handle);
  [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr handle,uint command);
  static bool IsAbove(IntPtr first,IntPtr second) {
    IntPtr current=GetWindow(second,3);int count=0;
    while(current!=IntPtr.Zero && count++<10000) {if(current==first)return true;current=GetWindow(current,3);}return false;
  }
  static void MenuPlacementTest(OverlayWindow window) {
    var menu=Field<ContextMenu>(window,"optionsMenu");
    Check(menu.Placement==System.Windows.Controls.Primitives.PlacementMode.Right && Object.ReferenceEquals(menu.PlacementTarget,Field<Border>(window,"panel")),"context menu must anchor beside the entire panel, not at the click point");
    Check(menu.HorizontalOffset>=6,"context menu must leave a visible gap from the panel");
    Known(window);Console.WriteLine("PASS native menu-placement: side anchoring and gap prevent click-point overlap");
  }
  static void MenuLayerTest(OverlayWindow window) {
    var apply=typeof(OverlayWindow).GetMethod("ApplyNativePlacement",BindingFlags.Static|BindingFlags.NonPublic);
    Check(apply!=null,"native placement must expose the production move operation for an own-window integration test");
    IntPtr overlay=IntPtr.Zero,menu=IntPtr.Zero;
    try {
      // Only our two synthetic HWNDs, offscreen and WS_EX_NOACTIVATE. No external
      // application is clicked, moved, activated or captured by this check.
      overlay=CreateWindowEx(0x08000088,"STATIC","Quota own overlay test",0x80000000,-32000,-32000,46,192,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
      menu=CreateWindowEx(0x08000088,"STATIC","Quota own menu test",0x80000000,-31980,-32000,240,240,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
      Check(overlay!=IntPtr.Zero && menu!=IntPtr.Zero,"own test HWND creation must succeed");
      Native.SetWindowPos(overlay,new IntPtr(-1),-32000,-32000,46,192,0x10|0x40);
      Native.SetWindowPos(menu,new IntPtr(-1),-31980,-32000,240,240,0x10|0x40);
      Check(IsAbove(menu,overlay),"test menu must initially be above the panel");
      var placement=new Placement {X=-32000,Y=-32000,Width=46,Height=192};
      for(int tick=0;tick<4;tick++)apply.Invoke(null,new object[]{overlay,placement,true});
      Check(IsAbove(menu,overlay),"tracker ticks must not raise the panel over its open menu");
      apply.Invoke(null,new object[]{overlay,placement,false});
      Check(IsAbove(overlay,menu),"normal closed-menu placement must retain topmost following behavior");
    }finally {if(menu!=IntPtr.Zero)DestroyWindow(menu);if(overlay!=IntPtr.Zero)DestroyWindow(overlay);}
    Known(window);Console.WriteLine("PASS native menu-layer: actual offscreen HWND ordering survives tracker updates");
  }
  static void MenuDismissTest(OverlayWindow window) {
    var type=typeof(OverlayWindow).Assembly.GetType("QuotaOverlay.MenuDismissWatcher");
    Check(type!=null,"menu must observe outside clicks independently of WPF focus/capture");
    bool open=true;int dismissed=0,lookups=0;uint own=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;
    Func<Native.Point,uint> lookup=delegate(Native.Point point){lookups++;return point.X==Int32.MinValue+101?own+1:own;};
    var watcher=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Dispatcher.CurrentDispatcher,new Func<bool>(delegate{return open;}),new Action(delegate{open=false;dismissed++;}),lookup},null);
    var start=type.GetMethod("Start");var stop=type.GetMethod("Stop");var observe=type.GetMethod("Observe",BindingFlags.Instance|BindingFlags.NonPublic);var hook=type.GetProperty("Hook");
    IntPtr data=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.Point)));
    try {
      start.Invoke(watcher,null);IntPtr installed=(IntPtr)hook.GetValue(watcher,null);Check(installed!=IntPtr.Zero,"menu-open watcher must register its native observer");
      start.Invoke(watcher,null);Check((IntPtr)hook.GetValue(watcher,null)==installed,"repeated menu open must not install duplicate observers");
      Marshal.StructureToPtr(new Native.Point {X=Int32.MinValue+100,Y=Int32.MinValue+200},data,false);
      observe.Invoke(watcher,new object[]{0,new IntPtr(0x201),data});PumpDispatcher();
      Check(open && dismissed==0,"clicking the badge or an own popup/submenu must keep menu navigation intact");
      Marshal.StructureToPtr(new Native.Point {X=Int32.MinValue+101,Y=Int32.MinValue+200},data,false);
      observe.Invoke(watcher,new object[]{0,new IntPtr(0x201),data});PumpDispatcher();
      Check(!open && dismissed==1,"button-down over another process, including its blank sidebar, must dismiss once");
      open=true;int before=lookups;
      observe.Invoke(watcher,new object[]{-1,new IntPtr(0x201),IntPtr.Zero});
      observe.Invoke(watcher,new object[]{0,new IntPtr(0x200),IntPtr.Zero});PumpDispatcher();
      Check(open && lookups==before,"mouse movement and negative hook codes must not inspect or dismiss anything");
      observe.Invoke(watcher,new object[]{0,new IntPtr(0x204),data});stop.Invoke(watcher,null);start.Invoke(watcher,null);PumpDispatcher();
      Check(open && dismissed==1,"a queued old click must not dismiss a reopened menu session");
      stop.Invoke(watcher,null);Check((IntPtr)hook.GetValue(watcher,null)==IntPtr.Zero,"closed menus must release their native observer");
      var menu=Field<ContextMenu>(window,"optionsMenu");var bound=Field<object>(window,"menuDismiss");
      menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
      Check((IntPtr)hook.GetValue(bound,null)!=IntPtr.Zero,"production menu Opened must attach its observer");
      menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));
      Check((IntPtr)hook.GetValue(bound,null)==IntPtr.Zero,"production menu Closed must detach its observer");
    }finally{stop.Invoke(watcher,null);Marshal.FreeHGlobal(data);}
    Console.WriteLine("PASS native menu-dismiss: outside-click dispatch, own submenus, generation guard and native observer lifecycle");
  }
  static void CombinedZeroTest(ref OverlayWindow window,string directory) {
    Invoke(window,"SetCompact",false);MenuClick(Field<MenuItem>(window,"fullBothItem"));
    Apply(window,Row("5 ч","0%","25.10","06:13","","critical"),Row("Нед.","25%","31.10","20:10","","warning"));
    var outline=Field<System.Windows.Shapes.Path>(window,"progressOutline");
    var tracks=Field<Border[]>(window,"progressTracks");var fills=Field<Border[]>(window,"progressFills");
    Check(tracks[0].Visibility==Visibility.Visible && tracks[1].Visibility==Visibility.Visible && outline.Visibility==Visibility.Visible,"combined mode must display bars and outline simultaneously");
    Check(fills[0].Width==32 && ColorOf(fills[0].Background)==ParseColor("#E6A0A1"),"known zero quota must render a full red warning bar");
    Check(Math.Abs(fills[1].Width-8)<.01,"nonzero quota must retain its actual fraction");
    Check(ColorOf(outline.Stroke)==ParseColor("#E6A0A1") && Math.Abs(outline.Data.Bounds.Bottom-(Layout.PanelHeight-1.25))<.1,"known zero selected quota must render a full red frame");
    Check(Field<TextBlock[]>(window,"values")[0].Text=="0%","warning graphics must never change the numeric zero value");
    MenuClick(Field<MenuItem>(window,"weeklyProgressItem"));
    Check(ColorOf(outline.Stroke)==ParseColor("#E3B382"),"combined source selection must still follow weekly quota");
    window.Close();window=NewWindow(Path.Combine(directory,"state.json"));AssertUnshown(window);
    Check(Field<string>(window,"fullProgressMode")=="both" && Field<string>(window,"contourWindow")=="weekly","combined mode and source must persist");
    MenuClick(Field<MenuItem>(window,"shortProgressItem"));
    Invoke(window,"SetCompact",true);
    Apply(window,Row("5 ч","0%","25.10","06:13","","critical"),Row("Нед.","25%","31.10","20:10","","warning"));
    outline=Field<System.Windows.Shapes.Path>(window,"progressOutline");
    Check(ColorOf(outline.Stroke)==ParseColor("#E6A0A1") && Math.Abs(outline.Data.Bounds.Bottom-98.75)<.1,"compact zero must also show a complete red warning frame");
    Apply(window,Row("5 ч","—","—","—","","unknown"),Row("Нед.","—","—","—","","unknown"));
    Check(outline.Data.IsEmpty() && Field<Border[]>(window,"progressFills")[0].Width==0,"unknown quota must not produce the exhausted warning");
    MenuClick(Field<MenuItem>(window,"fullOffItem"));
    Apply(window,Row("5 ч","0%","25.10","06:13","","critical"),Row("Нед.","0%","31.10","20:10","","critical"));
    Check(outline.Visibility==Visibility.Collapsed,"zero must respect an explicitly disabled contour");
    Console.WriteLine("PASS native combined-zero: simultaneous indicators and full red exhausted warning distinct from off and unknown");
  }
  static void ResizeAspectTest(OverlayWindow window) {
    Known(window);
    var root=(FrameworkElement)window.Content;var surface=Field<Grid>(window,"surface");
    foreach(double scale in new[]{1.0,1.25,1.5,2.0})foreach(bool compactMode in new[]{false,true,false,true}) {
      Invoke(window,"SetCompact",compactMode);
      // Simulate the frame where HWND/viewport still has the previous mode's height.
      double oldHeight=(compactMode?Layout.PanelHeight:Layout.CompactHeight)*scale;
      root.Measure(new Size(46*scale,oldHeight));root.Arrange(new Rect(0,0,46*scale,oldHeight));root.UpdateLayout();
      Rect rendered=surface.TransformToAncestor(root).TransformBounds(new Rect(new Point(0,0),surface.RenderSize));
      double expectedHeight=(compactMode?100:Layout.PanelHeight)*scale;
      Check(Math.Abs(rendered.Width-46*scale)<.1 && Math.Abs(rendered.Height-expectedHeight)<.1,
        "intermediate viewport must not stretch or shrink the badge; actual "+rendered+" expected height "+expectedHeight);
      Check(Math.Abs(rendered.Bottom-oldHeight)<.1,"badge must keep its bottom anchor during intermediate layout");
    }
    Console.WriteLine("PASS native resize-aspect: both directions and four DPI scales preserve glyph/frame proportions in mismatched viewports");
  }
  static void ResizeNativeTest(OverlayWindow window) {
    var handle=new WindowInteropHelper(window).EnsureHandle();AssertUnshown(window,true);
    Field<DispatcherTimer>(window,"tracker").Stop();
    var hook=Field<IntPtr>(window,"eventHook");if(hook!=IntPtr.Zero){Native.UnhookWinEvent(hook);typeof(OverlayWindow).GetField("eventHook",PrivateInstance).SetValue(window,IntPtr.Zero);}
    Native.Rect before,after;Native.GetClientRect(handle,out before);
    Invoke(window,"SetCompact",false);Native.GetClientRect(handle,out after);
    Check(before.Bottom-before.Top==after.Bottom-after.Top,"content change must not separately resize a live HWND before the atomic placement");
    Console.WriteLine("PASS native resize-native: content change waits for one authoritative native size/position update");
  }
  static void UltraTest(OverlayWindow window) {
    MenuClick(Field<MenuItem>(window,"fullBothItem"));
    Invoke(window,"SelectMode","ultra");
    Apply(window,Row("5 ч","25%","25.10","14:59","","warning"),Row("Нед.","0%","01.11","09:59","","critical"));
    var panel=Field<Border>(window,"panel");
    Check(panel.Height==32,"ultra must use a 32 DIP frame");
    foreach(string group in new[]{"values","dates","times","periods","markers"})foreach(var text in Field<TextBlock[]>(window,group))Check(text.Visibility==Visibility.Collapsed,"ultra must have no quota/reset text");
    Check(Field<TextBlock>(window,"compactStale").Visibility==Visibility.Collapsed && Field<Border>(window,"divider").Visibility==Visibility.Collapsed,"ultra must contain only two bars inside its frame");
    panel.Measure(new Size(46,32));panel.Arrange(new Rect(0,0,46,32));panel.UpdateLayout();
    var tracks=Field<Border[]>(window,"progressTracks");var fills=Field<Border[]>(window,"progressFills");
    Rect upper=tracks[0].TransformToAncestor(panel).TransformBounds(new Rect(new Point(),tracks[0].RenderSize));
    Rect lower=tracks[1].TransformToAncestor(panel).TransformBounds(new Rect(new Point(),tracks[1].RenderSize));
    Check(tracks[0].Visibility==Visibility.Visible && tracks[1].Visibility==Visibility.Visible && upper.Top>0 && upper.Bottom<lower.Top && lower.Bottom<32,"both separated ultra bars must fit in the frame");
    Check(fills[0].Width==8 && fills[1].Width==32 && ColorOf(fills[1].Background)==ParseColor("#E6A0A1"),"ultra bars must show each quota, including full red known-zero warning");
    var outline=Field<System.Windows.Shapes.Path>(window,"progressOutline");
    Check(outline.Visibility==Visibility.Visible && outline.Height==32 && ColorOf(outline.Stroke)==ParseColor("#E3B382"),"ultra default contour must represent short quota");
    MenuClick(Field<MenuItem>(window,"weeklyProgressItem"));
    Check(ColorOf(outline.Stroke)==ParseColor("#E6A0A1") && outline.Data.Bounds.Bottom>30,"ultra weekly zero must draw a complete red frame");
    Apply(window,Row("5 ч","—","—","—","","unknown"),Row("Нед.","—","—","—","","unknown"));
    Check(fills[0].Width==0 && fills[1].Width==0 && outline.Data.IsEmpty(),"ultra unknown must not fabricate remaining quota");
    Apply(window,Row("5 ч","50%","25.10","14:59","устар.","normal"),Row("Нед.","75%","01.11","09:59","устар.","normal"));
    Check(tracks[0].Opacity<1 && outline.Opacity<1 && Field<TextBlock>(window,"compactStale").Visibility==Visibility.Collapsed,"ultra stale values must dim without adding text");
    MenuClick(Field<MenuItem>(window,"fullOffItem"));
    Check(outline.Visibility==Visibility.Collapsed && tracks[0].Visibility==Visibility.Visible,"ultra contour switch must preserve both bars");
    Console.WriteLine("PASS native ultra: two stacked bars, no text, selected frame, zero, unknown and stale states");
  }
  static void SharedIndicatorTest(OverlayWindow window) {
    var menu=Field<ContextMenu>(window,"optionsMenu");MenuItem shared=null;
    foreach(object entry in menu.Items) {
      var item=entry as MenuItem;if(item==null)continue;
      string header=Convert.ToString(item.Header);
      Check(header!="Контур в компактном виде" && header!="Ультракомпактный вид","separate contour settings must be removed");
      if(header=="Индикатор")shared=item;
    }
    Check(shared!=null && shared.Items.Count==4,"one visible indicator menu must contain the four exclusive choices");
    Known(window);
    foreach(string choice in new[]{"bars","outline","both","off"}) {
      MenuClick(Field<MenuItem>(window,choice=="bars"?"fullProgressItem":choice=="outline"?"fullOutlineItem":choice=="both"?"fullBothItem":"fullOffItem"));
      foreach(string mode in new[]{"full","compact","ultra"}) {
        Invoke(window,"SetMode",mode);
        bool bars=mode=="ultra"||choice=="bars"||choice=="both",outline=choice=="outline"||choice=="both";
        var tracks=Field<Border[]>(window,"progressTracks");
        Check((tracks[0].Visibility==Visibility.Visible)==bars && (tracks[1].Visibility==Visibility.Visible)==bars,"shared bar choice must apply to "+mode+"/"+choice);
        Check((Field<System.Windows.Shapes.Path>(window,"progressOutline").Visibility==Visibility.Visible)==outline,"shared outline choice must apply to "+mode+"/"+choice);
        if(mode=="compact") {
          AssertCompactFits(window);
          var panel=Field<Border>(window,"panel");
          var resets=new[]{Field<TextBlock[]>(window,"times")[0],Field<TextBlock[]>(window,"dates")[1]};
          if(bars)for(int i=0;i<2;i++) {
            Rect bounds=tracks[i].TransformToAncestor(panel).TransformBounds(new Rect(new Point(),tracks[i].RenderSize));
            Rect reset=resets[i].TransformToAncestor(panel).TransformBounds(new Rect(new Point(),resets[i].RenderSize));
            Check(bounds.Top>=reset.Bottom && bounds.Bottom<=100-panel.Padding.Bottom-panel.BorderThickness.Bottom,"compact bars must fit under their reset labels");
          }
        }
      }
    }
    Console.WriteLine("PASS native shared-indicator: all four choices apply across modes, ultra bar exception, compact stale text/bar fit");
  }
  static void ThemeTest(OverlayWindow window) {
    Invoke(window,"SelectTheme","dark");Invoke(window,"SelectMode","full");MenuClick(Field<MenuItem>(window,"fullBothItem"));
    Apply(window,Row("5 ч","30%","25.10","14:59","","warning"),Row("Нед.","10%","01.11","09:59","","critical"));
    AssertReadableColor(window,0,"#E3B382");AssertReadableColor(window,1,"#E6A0A1");
    Invoke(window,"SelectTheme","light");
    Check(Field<string>(window,"effectiveTheme")=="light" && ColorOf(Field<Border>(window,"panel").Background)==ParseColor("#F3F3F4"),"manual light must apply a light panel surface");
    AssertReadableColor(window,0,"#955416");AssertReadableColor(window,1,"#B23B42");
    var menu=Field<ContextMenu>(window,"optionsMenu");
    Check(ColorOf((Brush)menu.Resources["Quota.MenuSurface"])==ParseColor("#FFFFFF") && ColorOf((Brush)menu.Resources["Quota.MenuText"])==ParseColor("#25282D"),"menu resources must switch to readable light colours");
    menu.ApplyTemplate();var shell=VisualTreeHelper.GetChild(menu,0) as Border;
    Check(shell!=null && ColorOf(shell.Background)==ParseColor("#FFFFFF"),"the actual menu template must use live light palette resources");
    menu.Measure(new Size(500,800));menu.Arrange(new Rect(new Point(),menu.DesiredSize));menu.UpdateLayout();
    var menuImage=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth*2),(int)Math.Ceiling(menu.ActualHeight*2),192,192,PixelFormats.Pbgra32);menuImage.Render(menu);
    var menuEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();menuEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(menuImage));
    using(var output=File.Create(Path.Combine(Path.GetDirectoryName(Field<string>(window,"stateFile")),"theme-menu-light.png")))menuEncoder.Save(output);
    Check(Field<TextBlock[]>(window,"values")[0].Text=="30%" && Field<TextBlock[]>(window,"times")[0].Text=="14:59","theme changes must retain the quota and reset values");
    foreach(string mode in new[]{"full","compact","ultra"}) {
      Invoke(window,"SetMode",mode);
      Check(ColorOf(Field<System.Windows.Shapes.Path>(window,"progressOutline").Stroke)==ParseColor("#955416"),"all modes must use light warning contour colours");
    }
    Apply(window,Row("5 ч","0%","25.10","14:59","","critical"),Row("Нед.","100%","01.11","09:59","","normal"));
    Check(Field<Border[]>(window,"progressFills")[0].Width==32 && ColorOf(Field<System.Windows.Shapes.Path>(window,"progressOutline").Stroke)==ParseColor("#B23B42"),"known zero must retain its full red bar/contour warning in light mode");
    AssertReadableColor(window,1,"#25282D");
    Apply(window,Row("5 ч","—","—","—","","unknown"),Row("Нед.","—","—","—","","unknown"));
    AssertReadableColor(window,0,"#626873");
    Apply(window,Row("5 ч","30%","25.10","14:59","","warning"),Row("Нед.","10%","01.11","09:59","","critical"));
    Invoke(window,"SelectTheme","dark");
    Check(ColorOf(shell.Background)==ParseColor("#292A2D"),"an already constructed menu template must update without being recreated");
    Check(ColorOf(Field<Border>(window,"panel").Background)==ParseColor("#242526"),"dark background must retain its original colour");
    AssertReadableColor(window,0,"#E3B382");AssertReadableColor(window,1,"#E6A0A1");
    Console.WriteLine("PASS native theme: light/dark surfaces, readable warnings, menu resources and unchanged quota values");
  }
  static void ThemeAutoTest(ref OverlayWindow window,string directory) {
    window.Close();string config=Path.Combine(directory,"controlled-app.toml");bool windowsLight=true;
    File.WriteAllText(config,"[desktop]\nappearanceTheme = \"dark\"\n");
    window=new OverlayWindow(Path.Combine(directory,"state.json"),new ThemeResolver(config,delegate{return windowsLight;}));
    Known(window);Check(Field<string>(window,"themeMode")=="auto" && Field<string>(window,"effectiveTheme")=="dark","auto must prefer explicit app dark over Windows light");
    windowsLight=false;File.WriteAllText(config,"[desktop]\nappearanceTheme = 'light'\n");Invoke(window,"RefreshTheme",true);
    Check(Field<string>(window,"effectiveTheme")=="light" && Field<TextBlock[]>(window,"values")[0].Text=="100%","app light must override Windows dark without another quota update");
    File.WriteAllText(config,"[desktop]\nappearanceTheme = 'system'\n");Invoke(window,"RefreshTheme",true);
    Check(Field<string>(window,"effectiveTheme")=="dark","app system must follow Windows dark");
    windowsLight=true;Invoke(window,"RefreshTheme",true);Check(Field<string>(window,"effectiveTheme")=="light","system changes must refresh auto without restarting");
    MenuClick(Field<MenuItem>(window,"themeDarkItem"));Invoke(window,"RefreshTheme",true);Check(Field<string>(window,"effectiveTheme")=="dark","manual dark must override app/Windows light");
    File.WriteAllText(config,"[desktop]\nappearanceTheme = 'dark'\n");MenuClick(Field<MenuItem>(window,"themeLightItem"));Invoke(window,"RefreshTheme",true);
    Check(Field<string>(window,"effectiveTheme")=="light","manual light must override app dark");
    MenuClick(Field<MenuItem>(window,"themeAutoItem"));Check(Field<string>(window,"effectiveTheme")=="dark","returning to auto must immediately restore app preference");
    using(var locked=new FileStream(config,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Invoke(window,"RefreshTheme",true);
    Check(Field<string>(window,"effectiveTheme")=="dark","temporary read failures must retain the last known app choice");
    File.WriteAllText(config,"[desktop]\nappearanceTheme = 'light'\n");Invoke(window,"RefreshTheme",false);
    Check(Field<string>(window,"effectiveTheme")=="dark","tracker ticks must respect the theme polling interval");Invoke(window,"RefreshTheme",true);
    Check(Field<string>(window,"effectiveTheme")=="light","the reader must recover after a temporary file failure");
    Console.WriteLine("PASS native theme-auto: explicit app settings, live system fallback, manual overrides, read failure and recovery");
  }
  static void ThemeSettingsTest(ref OverlayWindow window,string directory) {
    File.WriteAllText(Path.Combine(directory,"settings.json"),"{\"other\":\"keep\",\"compact\":true}");
    MenuClick(Field<MenuItem>(window,"themeLightItem"));MenuClick(Field<MenuItem>(window,"themeLightItem"));
    Check(Field<MenuItem>(window,"themeLightItem").IsChecked && !Field<MenuItem>(window,"themeDarkItem").IsChecked && !Field<MenuItem>(window,"themeAutoItem").IsChecked,"theme radio choice cannot turn itself off");
    window.Close();window=NewWindow(Path.Combine(directory,"state.json"));
    Check(Field<string>(window,"themeMode")=="light" && Field<string>(window,"effectiveTheme")=="light","manual choice must survive reconstruction");
    var saved=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(directory,"settings.json"))) as Dictionary<string,object>;
    Check(Convert.ToString(saved["other"])=="keep","theme saving must preserve unrelated preferences");
    foreach(string fixture in new[]{"{}","{\"themeMode\":\"invalid\"}","{\"themeMode\":null}","{\"themeMode\":true}"}) {
      window.Close();File.WriteAllText(Path.Combine(directory,"settings.json"),fixture);window=NewWindow(Path.Combine(directory,"state.json"));
      Check(Field<string>(window,"themeMode")=="auto","invalid or missing theme preference must default to auto");
    }
    Console.WriteLine("PASS native theme-settings: persistent exclusive selection, invalid input defaults and unrelated preference preservation");
  }
  static void ThemeParserTest(OverlayWindow window) {
    string[,] cases={
      {"[desktop]\nappearanceTheme = \"light\" # chosen","light"},
      {"desktop.appearanceTheme='dark'","dark"},
      {"['desktop'] # section\n'appearanceTheme' = 'light'","light"},
      {"\uFEFF[desktop]\nappearanceTheme = '''light'''","light"},
      {"[other]\nappearanceTheme='light'","system"},
      {"[desktop.extra]\nappearanceTheme='light'","system"},
      {"note = '''\n[desktop]\nappearanceTheme='light'\n'''\n[desktop]\nappearanceTheme='dark'","dark"},
      {"note = \"\"\"\n[desktop]\nappearanceTheme='light'\n\"\"\"\n[desktop]\nappearanceTheme='system'","system"},
      {"note = \"\"\"C:\\\\\"\"\"\n[desktop]\nappearanceTheme='light'","light"},
      {"[desktop]\nnote = \"# appearanceTheme='light'\"\nappearanceTheme='dark'","dark"},
      {"[desktop]\nappearanceTheme='invalid'","system"},
      {"# [desktop]\n# appearanceTheme='light'","system"}
    };
    for(int i=0;i<cases.GetLength(0);i++)Check(ThemeResolver.ParseAppTheme(cases[i,0])==cases[i,1],"desktop-only appearance parser must handle fixture "+i);
    Console.WriteLine("PASS native theme-parser: scoped desktop setting, quoted keys, comments, BOM and multiline decoys");
  }
  static void StartupDelayTest(OverlayWindow window) {
    var type=typeof(OverlayWindow).Assembly.GetType("QuotaOverlay.AppStartupGate");Check(type!=null,"panel must have an app-opening readiness gate");
    object gate=Activator.CreateInstance(type);var update=type.GetMethod("Ready");
    Func<long,bool,long,bool> ready=delegate(long target,bool open,long now){return (bool)update.Invoke(gate,new object[]{new IntPtr(target),open,now});};
    Check(!ready(0,false,0),"no client window must remain hidden");
    Check(!ready(101,true,1000)&&!ready(101,true,3999)&&ready(101,true,4000),"new client must wait exactly three seconds");
    Check(ready(101,true,5000),"an already open client must stay ready on focus/minimize changes");
    Check(!ready(101,false,6000)&&!ready(101,true,7000)&&ready(101,true,10000),"closing/reopening must start a new delay");
    Check(!ready(202,true,10100)&&ready(202,true,13100),"a replacement client window must get its own startup delay");
    var item=Field<MenuItem>(window,"autostartItem");Check(item.IsCheckable && Convert.ToString(item.Header).Contains("Автозапуск"),"native menu must expose persistent autostart choice");
    Console.WriteLine("PASS native startup-delay: absent app, three-second threshold, reopening and settings entry");
  }
  static void UltraSettingsTest(ref OverlayWindow window,string directory) {
    Invoke(window,"SelectMode","full");ClickPanel(window);
    Check(Field<string>(window,"presentationMode")=="compact","full must cycle to compact");ClickPanel(window);
    Check(Field<string>(window,"presentationMode")=="ultra","compact must cycle to ultra");
    MenuClick(Field<MenuItem>(window,"weeklyProgressItem"));MenuClick(Field<MenuItem>(window,"fullOffItem"));
    window.Close();window=NewWindow(Path.Combine(directory,"state.json"));
    Check(Field<string>(window,"presentationMode")=="ultra" && Field<string>(window,"contourWindow")=="weekly" && !Field<bool>(window,"showUltraProgress"),"ultra mode and settings must survive reconstruction");
    Check(!Field<bool>(window,"showCompactProgress") && Field<string>(window,"contourWindow")=="weekly","shared off must survive across modes");
    MenuClick(Field<MenuItem>(window,"fullOutlineItem"));
    Apply(window,Row("5 ч","50%","25.10","14:59","","normal"),Row("Нед.","10%","01.11","09:59","","critical"));
    foreach(string mode in new[]{"full","compact","ultra"}) {
      Invoke(window,"SetMode",mode);
      Check(ColorOf(Field<System.Windows.Shapes.Path>(window,"progressOutline").Stroke)==ParseColor("#E6A0A1"),"one weekly contour choice must affect every mode");
    }
    ClickPanel(window);Check(Field<string>(window,"presentationMode")=="full","ultra must cycle back to full");
    MenuClick(Field<MenuItem>(window,"ultraModeItem"));Check(Field<string>(window,"presentationMode")=="ultra","menu must directly select ultra");
    MenuClick(Field<MenuItem>(window,"compactModeItem"));MenuClick(Field<MenuItem>(window,"compactModeItem"));Check(Field<string>(window,"presentationMode")=="compact","repeated direct selection must keep the selected mode");
    window.Close();File.WriteAllText(Path.Combine(directory,"settings.json"),"{\"compact\":false,\"other\":\"preserve\",\"displayMode\":\"invalid\"}");window=NewWindow(Path.Combine(directory,"state.json"));
    Check(Field<string>(window,"presentationMode")=="full","invalid explicit mode must fall back to legacy compact preference");
    Invoke(window,"SelectMode","ultra");
    var saved=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(directory,"settings.json"))) as Dictionary<string,object>;
    Check(Convert.ToString(saved["displayMode"])=="ultra" && Convert.ToString(saved["other"])=="preserve","mode saving must retain unrelated preferences");
    Console.WriteLine("PASS native ultra-settings: cycle, direct selection, shared source/indicators and legacy migration");
  }
  static void ViewportTest(OverlayWindow window,string directory) {
    // Exercise the real WPF Window -> presenter -> host -> badge path. Only this
    // process's offscreen, no-activate window is shown; no external UI is touched.
    Field<DispatcherTimer>(window,"tracker").Stop();
    var hook=Field<IntPtr>(window,"eventHook");if(hook!=IntPtr.Zero){Native.UnhookWinEvent(hook);typeof(OverlayWindow).GetField("eventHook",PrivateInstance).SetValue(window,IntPtr.Zero);}
    typeof(OverlayWindow).GetField("started",PrivateInstance).SetValue(window,true);
    Apply(window,Row("5 ч","56%","25.10","14:59","","normal"),Row("Нед.","93%","01.11","09:59","","normal"));
    window.Left=-32000;window.Top=-32000;window.Show();
    var handle=Field<IntPtr>(window,"hwnd");
    foreach(double scale in new[]{1.0,1.25,1.5,2.0})foreach(string choice in new[]{"bars","outline","both","off"})foreach(string mode in new[]{"compact","full","ultra","compact","ultra","full","compact"}) {
      Invoke(window,"SelectFullProgress",choice);
      Invoke(window,"SetMode",mode);
      var placement=new Placement {X=-32000,Y=-32000,Width=46*scale,Height=(mode=="ultra"?32:mode=="compact"?100:Layout.PanelHeight)*scale};
      OverlayWindow.ApplyNativePlacement(handle,placement,true);
      window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));window.UpdateLayout();
      Invoke(window,"SavePreview");
      window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));window.UpdateLayout();
      var host=(FrameworkElement)window.Content;var surface=Field<Grid>(window,"surface");
      Native.Rect client;Native.GetClientRect(handle,out client);
      var source=HwndSource.FromHwnd(handle);double dpi=source.CompositionTarget.TransformToDevice.M22;
      Rect badge=surface.TransformToAncestor(window).TransformBounds(new Rect(new Point(0,0),surface.RenderSize));
      var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(window.ActualWidth*3)),Math.Max(1,(int)Math.Ceiling(window.ActualHeight*3)),288,288,PixelFormats.Pbgra32);bitmap.Render(window);
      var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
      using(var stream=File.Create(Path.Combine(directory,"viewport-"+scale+"-"+mode+"-"+choice+".png")))encoder.Save(stream);
      var labels=new List<TextBlock>();foreach(string group in new[]{"values","dates","times","periods"})labels.AddRange(Field<TextBlock[]>(window,group));
      foreach(var label in labels) {
        if(label.Visibility!=Visibility.Visible)continue;
        Rect bounds=label.TransformToAncestor(window).TransformBounds(new Rect(new Point(0,0),label.RenderSize));
        Check(bounds.Left>=-.1 && bounds.Right<=client.Right/dpi+.1 && bounds.Top>=-.1 && bounds.Bottom<=client.Bottom/dpi+.1,"all visible quota labels must fit the actual native viewport; label="+label.Text+" bounds="+bounds+" native height="+client.Bottom/dpi);
      }
      foreach(var bar in Field<Border[]>(window,"progressTracks"))if(bar.Visibility==Visibility.Visible) {
        Rect bounds=bar.TransformToAncestor(window).TransformBounds(new Rect(new Point(),bar.RenderSize));
        Check(bounds.Top>=0 && bounds.Bottom<=client.Bottom/dpi+.01,"visible progress bars must fit actual viewport in every mode");
      }
      Check(badge.Top>=-.01 && Math.Abs(badge.Bottom-client.Bottom/dpi)<.1,"settled frame must fit the actual viewport without clipping its top edge at fractional scale");
    }
    window.Hide();
    Console.WriteLine("PASS native "+(Field<string>(window,"effectiveTheme")=="light"?"viewport-light":"viewport")+": real window composition keeps all modes and graphics inside the native client area");
  }
  [STAThread] public static int Main(string[] args) {
    Application app=null;OverlayWindow window=null;
    try {
      Check(args.Length==2,"expected scenario and unique temporary state directory");
      string directory=Path.GetFullPath(args[1]);Directory.CreateDirectory(directory);
      app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
      window=NewWindow(Path.Combine(directory,"state.json"));
      AssertUnshown(window);
      switch(args[0]) {
        case "layout":LayoutTest(window);break;
        case "tones":ToneTest(window);break;
        case "unknown":UnknownTest(window);break;
        case "protocol":ProtocolTest(window);break;
        case "compact":CompactTest(window);break;
        case "ultra":UltraTest(window);break;
        case "ultra-settings":UltraSettingsTest(ref window,directory);break;
        case "shared-indicator":SharedIndicatorTest(window);break;
        case "theme":ThemeTest(window);break;
        case "theme-auto":ThemeAutoTest(ref window,directory);break;
        case "theme-settings":ThemeSettingsTest(ref window,directory);break;
        case "theme-parser":ThemeParserTest(window);break;
        case "startup-delay":StartupDelayTest(window);break;
        case "toggle":ToggleTest(ref window,directory);break;
        case "settings":SettingsTest(ref window,directory);break;
        case "input":InputTest(window);break;
        case "styles":StylesTest(window);break;
        case "tooltip":TooltipTest(window);break;
        case "progress":ProgressTest(window);break;
        case "progress-settings":ProgressSettingsTest(ref window,directory);break;
        case "contour":ContourTest(window);break;
        case "menu-actions":MenuActionsTest(window);break;
        case "full-indicator":FullIndicatorTest(ref window,directory);break;
        case "full-migration":FullMigrationTest(ref window,directory);break;
        case "menu-placement":MenuPlacementTest(window);break;
        case "menu-dismiss":MenuDismissTest(window);break;
        case "menu-layer":MenuLayerTest(window);break;
        case "combined-zero":CombinedZeroTest(ref window,directory);break;
        case "resize-aspect":ResizeAspectTest(window);break;
        case "resize-native":ResizeNativeTest(window);break;
        case "viewport":ViewportTest(window,directory);break;
        case "viewport-light":Invoke(window,"SelectTheme","light");ViewportTest(window,directory);break;
        default:throw new Exception("unknown native test scenario "+args[0]);
      }
      AssertUnshown(window,args[0]=="styles"||args[0]=="resize-native"||args[0]=="viewport"||args[0]=="viewport-light");
      return 0;
    } catch(Exception error) {
      // Avoid an unhandled .NET Framework exception invoking Windows Error Reporting.
      Console.Error.WriteLine(error.ToString());return 1;
    } finally {
      try {if(window!=null)window.Close();}catch(Exception error){Console.Error.WriteLine(error.ToString());}
      try {if(app!=null)app.Shutdown();}catch(Exception error){Console.Error.WriteLine(error.ToString());}
    }
  }
}
