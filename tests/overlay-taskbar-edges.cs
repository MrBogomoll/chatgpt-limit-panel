using System;
using QuotaOverlay;

internal static class TaskbarEdgesTest {
  static int assertions;
  static void Check(bool value,string message) {assertions++;if(!value)throw new Exception(message);}
  static bool Near(double a,double b) {return Math.Abs(a-b)<0.01;}
  static TaskbarArea Area(double x,double y,double width,double height) {return new TaskbarArea{X=x,Y=y,Width=width,Height=height};}
  static TaskbarSnapshot Snapshot(TaskbarArea area,double scale=1) {return new TaskbarSnapshot{Bounds=area,Scale=scale,Visible=true,OccupancyKnown=true};}
  static bool Intersects(TaskbarArea a,TaskbarArea b) {return a.X<b.X+b.Width&&a.X+a.Width>b.X&&a.Y<b.Y+b.Height&&a.Y+a.Height>b.Y;}
  static void Fits(TaskbarSnapshot snapshot,TaskbarPlacement placement,string message) {
    Check(placement.Visible,message+" must show");
    TaskbarArea bar=snapshot.Bounds,bounds=placement.Bounds;
    Check(bounds.X>=bar.X&&bounds.Y>=bar.Y&&bounds.X+bounds.Width<=bar.X+bar.Width&&bounds.Y+bounds.Height<=bar.Y+bar.Height,message+" must stay inside its taskbar");
    Check(Near(bounds.Width,Math.Ceiling(48*snapshot.Scale))&&Near(bounds.Height,Math.Ceiling(32*snapshot.Scale)),message+" must keep its upright two-row size");
    foreach(TaskbarArea item in snapshot.Occupied)Check(!Intersects(item,bounds),message+" must protect taskbar controls and widgets");
  }
  static void HorizontalEdges() {
    foreach(double y in new[]{0.0,1032.0}) {
      var snapshot=Snapshot(Area(0,y,1920,48));
      snapshot.Occupied.Add(Area(0,y,120,48));
      snapshot.Occupied.Add(Area(800,y,320,48));
      snapshot.Occupied.Add(Area(1650,y,270,48));
      var first=TaskbarLayout.Place(snapshot,"left");var last=TaskbarLayout.Place(snapshot,"right");
      Fits(snapshot,first,"horizontal start");Fits(snapshot,last,"horizontal end");
      Check(first.Bounds.X+first.Bounds.Width<960&&last.Bounds.X>960,"horizontal preferences must stay in their requested halves");
      Check(Near(first.Bounds.Y,y+8)&&Near(last.Bounds.Y,y+8),"both horizontal edges must center upright labels in the row");
    }
  }
  static void VerticalEdges() {
    foreach(double x in new[]{0.0,1872.0}) {
      var snapshot=Snapshot(Area(x,0,48,1080));
      snapshot.Occupied.Add(Area(x,0,48,120));
      snapshot.Occupied.Add(Area(x,430,48,180));
      snapshot.Occupied.Add(Area(x,900,48,180));
      var first=TaskbarLayout.Place(snapshot,"left");var last=TaskbarLayout.Place(snapshot,"right");
      Fits(snapshot,first,"vertical start");Fits(snapshot,last,"vertical end");
      Check(first.Bounds.Y+first.Bounds.Height<540&&last.Bounds.Y>540,"vertical preferences must map to top and bottom halves");
      Check(Near(first.Bounds.X,x)&&Near(last.Bounds.X,x),"vertical48DIP row must not need extra cross-axis clearance");
      Check(first.Bounds.Y>=124&&last.Bounds.Y+last.Bounds.Height<=896,"vertical placement must clear Start, app buttons, NetWorx and tray regions");
    }
  }
  static void ScaledNegativeCoordinates() {
    var snapshot=Snapshot(Area(-1920,-1440,72,1440),1.5);
    snapshot.Occupied.Add(Area(-1920,-1440,72,100));
    snapshot.Occupied.Add(Area(-1920,-800,72,160));
    snapshot.Occupied.Add(Area(-1920,-200,72,200));
    var first=TaskbarLayout.Place(snapshot,"left");var last=TaskbarLayout.Place(snapshot,"right");
    Fits(snapshot,first,"scaled negative start");Fits(snapshot,last,"scaled negative end");
    Check(Near(first.Bounds.Width,72)&&Near(first.Bounds.Height,48),"per-monitor scaling must apply to both label axes");
    Check(first.Bounds.Y+first.Bounds.Height<=-726&&last.Bounds.Y>=-714,"half-specific clearance must scale with DPI");
    var wide=Snapshot(Area(-2560,0,120,1440),1.5);
    Fits(wide,TaskbarLayout.Place(wide,"left"),"wider vertical taskbar");
    Check(Near(TaskbarLayout.Place(wide,"left").Bounds.X,-2536),"wide vertical rows must center the label horizontally");
  }
  static void ProtectedAndUnknown() {
    var snapshot=Snapshot(Area(0,0,48,1080));
    snapshot.Occupied.Add(Area(0,0,48,540));
    Fits(snapshot,TaskbarLayout.Place(snapshot,"left"),"vertical Start preference searches past the midpoint when its first half is occupied");
    Fits(snapshot,TaskbarLayout.Place(snapshot,"right"),"free second half");
    snapshot.Occupied.Add(Area(0,540,48,540));
    Check(!TaskbarLayout.Place(snapshot,"right").Visible,"full vertical taskbar must hide");
    snapshot=Snapshot(Area(0,0,48,1080));snapshot.Occupied.Add(Area(100,0,400,1080));
    Fits(snapshot,TaskbarLayout.Place(snapshot,"left"),"nonintersecting neighboring content");
    snapshot.Occupied.Add(Area(0,0,1,1080));
    Check(!TaskbarLayout.Place(snapshot,"left").Visible,"unknown thin painted region in the footprint stays protected");
    snapshot=Snapshot(Area(0,0,47,1080));Check(!TaskbarLayout.Place(snapshot,"left").Visible,"too-narrow vertical row must not clip percentages");
    snapshot=Snapshot(Area(0,0,48,1080));snapshot.OccupancyKnown=false;Check(!TaskbarLayout.Place(snapshot,"left").Visible,"vertical unknown occupancy hides");
    snapshot.OccupancyKnown=true;snapshot.AutoHidden=true;Check(!TaskbarLayout.Place(snapshot,"left").Visible,"vertical hidden auto-hide row hides");
    snapshot.AutoHidden=false;snapshot.Fullscreen=true;Check(!TaskbarLayout.Place(snapshot,"left").Visible,"vertical fullscreen guard remains active");
    snapshot.Fullscreen=false;snapshot.Occupied.Add(Area(0,Double.NaN,48,50));Check(!TaskbarLayout.Place(snapshot,"left").Visible,"malformed vertical obstacle hides");
  }
  static int Main() {
    try {HorizontalEdges();VerticalEdges();ScaledNegativeCoordinates();ProtectedAndUnknown();Console.WriteLine("PASS four taskbar edges ("+assertions+" assertions)");return 0;}
    catch(Exception error){Console.Error.WriteLine(error);return 1;}
  }
}
