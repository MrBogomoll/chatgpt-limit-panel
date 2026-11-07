using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaOverlay;

// Documentation renderer. Uses isolated temporary preferences and never shows a
// window, requests account data or captures the desktop. Values are illustrative.
class DocumentationPreviews {
  static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
  static void Call(object target,string name,params object[] args){target.GetType().GetMethod(name,Flags).Invoke(target,args);}
  static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,Flags).GetValue(target);}
  static Brush Brush(string color){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));}
  static void Save(BitmapSource image,string file){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var output=File.Create(file))encoder.Save(output);}
  static BitmapSource Render(Visual visual,double width,double height,int scale){var image=new RenderTargetBitmap((int)(width*scale),(int)(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);image.Render(visual);return image;}
  static BitmapSource RenderElement(FrameworkElement visual,double width,double height){visual.Measure(new Size(width,height));visual.Arrange(new Rect(0,0,width,height));visual.UpdateLayout();return Render(visual,width,height,3);}
  static object Row(string label,int value,string date,string time){return new Dictionary<string,object>{{"period",label},{"value",value+"%"},{"remaining",value},{"date",date},{"time",time},{"tone",value<=10?"critical":value<=30?"warning":"normal"},{"marker",""}};}
  static void Text(DrawingContext dc,string value,double x,double y,double size,Brush color,bool bold=false){var text=new FormattedText(value,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,color,1);dc.DrawText(text,new Point(x,y));}
  static BitmapSource Context(BitmapSource panel,bool light){
    var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
      Brush background=Brush(light?"#F7F7F7":"#171819"),rail=Brush(light?"#ECEDEF":"#202225"),text=Brush(light?"#33383D":"#E1E2E4"),muted=Brush(light?"#72777D":"#898E94");
      dc.DrawRectangle(background,null,new Rect(0,0,580,650));dc.DrawRectangle(rail,null,new Rect(0,0,52,650));
      dc.DrawRoundedRectangle(Brush(light?"#DCDDDF":"#34373B"),null,new Rect(8,22,36,36),10,10);Text(dc,"⌂",15,25,24,text,true);
      for(int i=0;i<6;i++){dc.DrawRoundedRectangle(null,new Pen(muted,1.6),new Rect(19,88+i*45,15,17),3,3);}
      Text(dc,"Codex",72,28,23,text,true);Text(dc,"⌕",420,25,26,muted);Text(dc,"Новый чат",78,96,17,text);Text(dc,"Закреплённые",72,155,15,muted,true);Text(dc,"Проекты",72,205,15,muted,true);Text(dc,"Недавние",72,255,15,muted,true);
      Text(dc,"Рабочий чат",78,298,16,text);Text(dc,"Идеи проекта",78,339,16,text);Text(dc,"Заметки",78,380,16,text);
      dc.DrawLine(new Pen(Brush(light?"#DADBDE":"#34373C"),1),new Point(492,0),new Point(492,650));
      double panelHeight=panel.PixelHeight*46.0/panel.PixelWidth;
      dc.DrawImage(panel,new Rect(3,566-panelHeight,46,panelHeight));dc.DrawEllipse(Brush(light?"#CED6DD":"#647583"),null,new Point(26,610),12,12);Text(dc,"M",20,600,14,Brush("#FFFFFF"),true);
      Text(dc,"Панель остаётся рядом с чатом",75,558,15,text,true);Text(dc,"Схема размещения · демонстрационные значения",75,590,11,muted);
    }return Render(visual,580,650,2);
  }
  static BitmapSource Taskbar(BitmapSource panel,bool light){
    var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
      var background=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(light?"#E8ECF2":"#10131C"),(Color)ColorConverter.ConvertFromString(light?"#CBD7E5":"#202939"),0);dc.DrawRectangle(background,null,new Rect(0,0,1024,88));
      Brush text=Brush(light?"#25303C":"#EAEFF5"),muted=Brush(light?"#647487":"#B9C5D2");
      for(int i=0;i<5;i++){dc.DrawRoundedRectangle(Brush(light?"#D0DAE5":"#303D50"),null,new Rect(24+i*55,24,32,32),7,7);}
      dc.DrawImage(panel,new Rect(625,20,72,48));
      Text(dc,"⌃",718,25,22,text);Text(dc,"РУС",759,30,15,text);dc.DrawRoundedRectangle(null,new Pen(muted,1.5),new Rect(814,31,19,14),1,1);dc.DrawLine(new Pen(muted,1.5),new Point(823,45),new Point(823,50));Text(dc,"♪",849,25,21,text);dc.DrawRoundedRectangle(null,new Pen(muted,1.5),new Rect(884,33,24,13),3,3);dc.DrawRectangle(Brush(light?"#478E72":"#B9E3C6"),null,new Rect(887,36,16,7));Text(dc,"12:05",940,17,16,text);Text(dc,"28.10.2026",924,44,13,text);
    }return Render(visual,1024,88,2);
  }
  [STAThread]static int Main(string[] args){try{
    string output=Path.GetFullPath(args[0]),temporary=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);Directory.CreateDirectory(temporary);Directory.CreateDirectory(Path.Combine(output,"panels"));int previews=0;
    foreach(string theme in new[]{"dark","light"}){
      BitmapSource full=null;
      foreach(string content in new[]{"limits","resets","credits","both"})foreach(string mode in new[]{"full","compact","ultra"})foreach(string indicator in mode=="ultra"?new[]{"both"}:new[]{"both","outline"}){
        bool selected=mode=="full"?(indicator=="both"?theme=="dark":theme=="light"):mode=="compact"?(indicator=="both"?theme=="light":theme=="dark"):((content=="limits"||content=="resets")?theme=="dark":theme=="light");
        bool legacy=content=="limits"&&indicator=="both";
        if(!selected&&!legacy)continue;
        bool resets=content=="resets"||content=="both",credits=content=="credits"||content=="both";
        string key=content+"-"+mode+"-"+indicator+"-"+theme,own=Path.Combine(temporary,key);Directory.CreateDirectory(own);
        var settings=new Dictionary<string,object>{{"language","ru"},{"themeMode",theme},{"displayMode",mode},{"indicatorMode",indicator},{"contourWindow","short"},{"sidebarBackgroundOpacity",100},{"showResets",resets},{"showCredits",credits},{"taskbarEnabled",false},{"liveTrayIcon",false},{"autoStartEnabled",false}};
        File.WriteAllText(Path.Combine(own,"settings.json"),new JavaScriptSerializer().Serialize(settings));
        var window=new OverlayWindow(Path.Combine(own,"state.json"),new ThemeResolver(Path.Combine(own,"app.toml"),delegate{return false;}),delegate{return false;},delegate{return null;});
        try{
          Call(window,"StopTracking");
          var resetData=new Dictionary<string,object>{{"availableCount",3},{"date","12.11.2026"},{"time","18:30"},{"daysRemaining",5},{"tone","warning"},{"stale",false}};
          var creditData=new Dictionary<string,object>{{"balance","250"},{"display","250"},{"unlimited",false},{"hasCredits",true},{"tone","normal"},{"stale",false}};
          int shortRemaining=key=="resets-full-both-dark"?5:key=="credits-full-both-dark"?0:key=="both-full-both-dark"?100:29;
          Call(window,"Apply",new Dictionary<string,object>{{"rows",new object[]{Row("5 ч",shortRemaining,"07.11","17:24"),Row("Нед.",75,"14.11","00:28")}},{"resets",resetData},{"credits",creditData}});Call(window,"SelectMode",mode);Call(window,"SelectFullProgress",indicator);
          var image=RenderElement(Field<Grid>(window,"surface"),46,Layout.HeightForMode(mode,resets,credits));
          if(selected){Save(image,Path.Combine(output,"panels",key+".png"));previews++;}
          if(legacy){Save(image,Path.Combine(output,(mode=="ultra"?"ultracompact":mode)+"-"+theme+".png"));if(mode=="full")full=image;}
        }finally{window.Close();}
      }
      var taskbar=new TaskbarWindow(delegate{return null;});try{taskbar.Configure(false,"right",theme=="light"?ThemePalette.Light:ThemePalette.Dark,50);taskbar.ApplyRows("38%","90%","normal","normal",false);var image=RenderElement(Field<Border>(taskbar,"surface"),48,32);Save(Taskbar(image,theme=="light"),Path.Combine(output,"taskbar-"+theme+".png"));}finally{taskbar.Dispose();}
      Save(Context(full,theme=="light"),Path.Combine(output,"sidebar-"+theme+".png"));
    }
    Console.WriteLine("Rendered "+previews+" comparison panels and 10 placement/legacy previews from production controls using isolated demonstration data.");return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
