using System;
namespace QuotaOverlay {
  public sealed class AppStartupGate {
    IntPtr watched;
    long readyAt;
    public const int DelayMilliseconds=3000;
    public bool Ready(IntPtr window,bool present,long nowMilliseconds) {
      if(!present||window==IntPtr.Zero){watched=IntPtr.Zero;return false;}
      if(watched!=window){watched=window;readyAt=nowMilliseconds+DelayMilliseconds;}
      return nowMilliseconds>=readyAt;
    }
  }
  public class Placement {
    public bool Visible,Compact;
    public string Mode;
    public double X,Y,Width,Height;
  }
  public static class Layout {
    public const double PanelWidth=46, PanelHeight=192, CompactHeight=100, UltraHeight=32, BottomClearance=110;
    // Keep within the current 52 DIP rail, leaving the update button, avatar and upper navigation free.
    public static Placement Place(double left,double top,double width,double height,double scale) {
      return Place(left,top,width,height,scale,false);
    }
    public static Placement Place(double left,double top,double width,double height,double scale,bool preferCompact) {
      return Place(left,top,width,height,scale,preferCompact?"compact":"full");
    }
    public static Placement Place(double left,double top,double width,double height,double scale,string preferredMode) {
      if(scale<=0 || double.IsNaN(scale)) scale=1;
      string mode=preferredMode=="ultra"?"ultra":preferredMode=="compact" || height/scale<760?"compact":"full";
      double panelHeight=mode=="ultra"?UltraHeight:mode=="compact"?CompactHeight:PanelHeight;
      return new Placement { Visible=width/scale>=320 && height/scale>=(mode=="ultra"?582:mode=="compact"?650:760),Compact=mode!="full",Mode=mode,
        X=left+3*scale,Y=top+height-(BottomClearance+panelHeight)*scale,
        Width=PanelWidth*scale,Height=panelHeight*scale };
    }
  }
}
