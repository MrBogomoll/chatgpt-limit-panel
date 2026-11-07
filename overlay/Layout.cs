using System;
using System.Collections.Generic;
namespace QuotaOverlay {
  public sealed class RailRegion {public double Top,Bottom;}
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
    public static double? PositionForBottom(double bottom,double top,double height,double panelHeight) {
      double extent=height-panelHeight;
      if(extent<=0||Double.IsNaN(bottom)||Double.IsInfinity(bottom))return null;
      return Math.Max(0,Math.Min(1,(bottom-top-panelHeight)/extent));
    }
    public static Placement PlaceFloating(double left,double top,double width,double height,double scale,string preferredMode,double? x,double? y,bool showResets=false,bool showCredits=false,int staleRows=0) {
      if(scale<=0||Double.IsNaN(scale)||Double.IsInfinity(scale))scale=1;
      double margin=4*scale,availableWidth=Math.Max(0,width-2*margin),availableHeight=Math.Max(0,height-2*margin);
      string mode=preferredMode=="ultra"?"ultra":preferredMode=="compact"?"compact":"full";
      double panelHeight=HeightForMode(mode,showResets,showCredits,staleRows)*scale;
      if(panelHeight>availableHeight&&mode=="full"){mode="compact";panelHeight=HeightForMode(mode,showResets,showCredits,staleRows)*scale;}
      if(panelHeight>availableHeight){mode="ultra";panelHeight=HeightForMode(mode,showResets,showCredits,staleRows)*scale;}
      double px=x.HasValue&&!Double.IsNaN(x.Value)&&!Double.IsInfinity(x.Value)?Math.Max(0,Math.Min(1,x.Value)):0;
      double py=y.HasValue&&!Double.IsNaN(y.Value)&&!Double.IsInfinity(y.Value)?Math.Max(0,Math.Min(1,y.Value)):Math.Max(0,Math.Min(1,(height-BottomClearance*scale-panelHeight-margin)/Math.Max(1,availableHeight-panelHeight)));
      return new Placement {Visible=availableWidth>=PanelWidth*scale&&availableHeight>=panelHeight,Mode=mode,Compact=mode!="full",Width=PanelWidth*scale,Height=panelHeight,
        X=left+margin+Math.Max(0,availableWidth-PanelWidth*scale)*px,Y=top+margin+Math.Max(0,availableHeight-panelHeight)*py};
    }
    public static Placement PlaceInAvailableRail(double left,double top,double width,double height,double scale,string preferredMode,IList<RailRegion> occupied,double? position,bool showResets=false,bool showCredits=false,int staleRows=0) {
      if(scale<=0||Double.IsNaN(scale)||Double.IsInfinity(scale))scale=1;
      var result=PlaceInRail(Place(left,top,width,height,scale,preferredMode,showResets,showCredits,staleRows),top,height,scale,occupied,position);
      if(!result.Visible&&preferredMode=="full")result=PlaceInRail(Place(left,top,width,height,scale,"compact",showResets,showCredits,staleRows),top,height,scale,occupied,position);
      if(!result.Visible&&occupied!=null) {
        var ultra=Place(left,top,width,height,scale,"ultra",showResets,showCredits,staleRows);
        // With measured controls, the actual free interval is authoritative;
        // retain minimum width and let PlaceInRail enforce header/control gaps.
        ultra.Visible=width/scale>=320;
        result=PlaceInRail(ultra,top,height,scale,occupied,position);
      }
      return result;
    }
    public static Placement PlaceInRail(Placement preferred,double top,double height,double scale,IList<RailRegion> occupied,double? position) {
      var result=new Placement{Visible=preferred.Visible,Compact=preferred.Compact,Mode=preferred.Mode,X=preferred.X,Y=preferred.Y,Width=preferred.Width,Height=preferred.Height};
      if(!result.Visible)return result;
      if(occupied==null){result.Visible=false;return result;}
      double margin=4*scale,start=48*scale,end=height-margin,cursor=start,best=0,distance=Double.PositiveInfinity;
      double desired=preferred.Y-top;
      if(position.HasValue&&!Double.IsNaN(position.Value)&&!Double.IsInfinity(position.Value)&&position.Value>=0&&position.Value<=1)desired=(height-result.Height)*position.Value;
      var spans=new List<RailRegion>();
      foreach(var obstacle in occupied){if(Double.IsNaN(obstacle.Top)||Double.IsNaN(obstacle.Bottom)||Double.IsInfinity(obstacle.Top)||Double.IsInfinity(obstacle.Bottom)||obstacle.Bottom<obstacle.Top){result.Visible=false;return result;}spans.Add(new RailRegion{Top=Math.Max(start,obstacle.Top-margin),Bottom=Math.Min(end,obstacle.Bottom+margin)});}
      spans.Sort(delegate(RailRegion a,RailRegion b){return a.Top.CompareTo(b.Top);});spans.Add(new RailRegion{Top=end,Bottom=end});
      foreach(var span in spans){if(span.Top-cursor>=result.Height){double candidate=Math.Max(cursor,Math.Min(span.Top-result.Height,desired)),difference=Math.Abs(candidate-desired);if(difference<distance){distance=difference;best=candidate;}}cursor=Math.Max(cursor,span.Bottom);}
      result.Visible=!Double.IsPositiveInfinity(distance);if(result.Visible)result.Y=top+best;return result;
    }
    public const double PanelWidth=46, PanelHeight=169, CompactHeight=115, UltraHeight=29, BottomClearance=110;
    public const double StaleHeaderHeight=10;
    public static double HeaderHeight(string mode) {return mode=="ultra"?0:StaleHeaderHeight;}
    public static double ResetsHeight(string mode) {return mode=="ultra"?22:mode=="compact"?41:64;}
    public static double CreditsHeight(string mode) {return mode=="ultra"?24:mode=="compact"?38:40;}
    public static double HeightForMode(string mode,bool showResets=false,bool showCredits=false,int staleRows=0) {
      return (mode=="ultra"?UltraHeight:mode=="compact"?CompactHeight:PanelHeight)+(showResets?ResetsHeight(mode)-HeaderHeight(mode):0)+(showCredits?CreditsHeight(mode):0);
    }
    // Keep within the current 52 DIP rail, leaving the update button, avatar and upper navigation free.
    public static Placement Place(double left,double top,double width,double height,double scale) {
      return Place(left,top,width,height,scale,false);
    }
    public static Placement Place(double left,double top,double width,double height,double scale,bool preferCompact) {
      return Place(left,top,width,height,scale,preferCompact?"compact":"full");
    }
    public static Placement Place(double left,double top,double width,double height,double scale,string preferredMode,bool showResets=false,bool showCredits=false,int staleRows=0) {
      if(scale<=0 || double.IsNaN(scale)) scale=1;
      double compactMinimum=650+HeightForMode("compact",showResets,showCredits,staleRows)-CompactHeight,fullMinimum=760+HeightForMode("full",showResets,showCredits,staleRows)-PanelHeight;
      string mode=preferredMode=="ultra"||height/scale<compactMinimum?"ultra":preferredMode=="compact"||height/scale<fullMinimum?"compact":"full";
      double panelHeight=HeightForMode(mode,showResets,showCredits,staleRows),minimum=(mode=="ultra"?582:mode=="compact"?650:760)+panelHeight-HeightForMode(mode);
      return new Placement { Visible=width/scale>=320 && height/scale>=minimum,Compact=mode!="full",Mode=mode,
        X=left+3*scale,Y=top+height-(BottomClearance+panelHeight)*scale,
        Width=PanelWidth*scale,Height=panelHeight*scale };
    }
  }
}
