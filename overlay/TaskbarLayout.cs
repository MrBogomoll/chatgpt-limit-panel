using System;
using System.Collections.Generic;

namespace QuotaOverlay {
  internal struct TaskbarArea {public double X,Y,Width,Height;}
  internal sealed class TaskbarSnapshot {
    public TaskbarArea Bounds;
    public double Scale=1;
    public bool Visible,AutoHidden,Fullscreen,OccupancyKnown;
    public IList<TaskbarArea> Occupied=new List<TaskbarArea>();
    public IntPtr Handle;
  }
  internal sealed class TaskbarPlacement {public bool Visible;public TaskbarArea Bounds;public string Reason;}

  internal static class TaskbarLayout {
    public const double Width=48,Height=32,Clearance=4;
    internal static bool Finite(double value) {return !Double.IsNaN(value)&&!Double.IsInfinity(value);}
    internal static bool Valid(TaskbarArea area) {return Finite(area.X)&&Finite(area.Y)&&Finite(area.Width)&&Finite(area.Height)&&area.Width>0&&area.Height>0&&Finite(area.X+area.Width)&&Finite(area.Y+area.Height);}
    static TaskbarPlacement Hidden(string reason) {return new TaskbarPlacement{Reason=reason};}
    public static TaskbarPlacement Place(TaskbarSnapshot snapshot,string side) {return Place(snapshot,side,null);}
    public static TaskbarPlacement Place(TaskbarSnapshot snapshot,string side,double? preferredPosition) {
      if(snapshot==null)return Hidden("unavailable");
      if(side!="left"&&side!="right")return Hidden("invalid-side");
      if(!snapshot.Visible)return Hidden("taskbar-hidden");
      if(snapshot.AutoHidden)return Hidden("autohide");
      if(snapshot.Fullscreen)return Hidden("fullscreen");
      if(!snapshot.OccupancyKnown||snapshot.Occupied==null)return Hidden("unknown-occupancy");
      if(!Valid(snapshot.Bounds)||!Finite(snapshot.Scale)||snapshot.Scale<=0||snapshot.Scale>8)return Hidden("invalid-geometry");
      TaskbarArea bar=snapshot.Bounds;double scale=snapshot.Scale;
      double width=Math.Ceiling(Width*scale),height=Math.Ceiling(Height*scale),margin=Math.Ceiling(Clearance*scale);
      bool vertical=bar.Height>bar.Width;
      if(bar.Width==bar.Height||(vertical?bar.Width<width:bar.Height<height))return Hidden("unsupported-taskbar");
      // Keep both percentage rows upright; only the gap-search axis changes.
      double cross=Math.Floor(vertical?bar.X+(bar.Width-width)/2:bar.Y+(bar.Height-height)/2);
      double crossSize=vertical?width:height,alongSize=vertical?height:width;
      double origin=vertical?bar.Y:bar.X,length=vertical?bar.Height:bar.Width;
      double middle=origin+length/2;
      // On a vertical bar the Start/application group can extend beyond the
      // midpoint. The preferred end controls search order, not a fictitious
      // occupied half of the screen.
      bool manual=preferredPosition.HasValue&&Finite(preferredPosition.Value)&&preferredPosition.Value>=0&&preferredPosition.Value<=1;
      double start=Math.Ceiling(manual||vertical||side=="left"?origin+margin:middle+margin);
      double end=Math.Floor(manual||vertical||side=="right"?origin+length-margin:middle-margin);
      var spans=new List<TaskbarArea>();
      foreach(TaskbarArea obstacle in snapshot.Occupied) {
        if(!Valid(obstacle))return Hidden("invalid-occupancy");
        double obstacleCross=vertical?obstacle.X:obstacle.Y,obstacleCrossSize=vertical?obstacle.Width:obstacle.Height;
        if(obstacleCross>=cross+crossSize+margin||obstacleCross+obstacleCrossSize<=cross-margin)continue;
        double obstacleAlong=vertical?obstacle.Y:obstacle.X,obstacleAlongSize=vertical?obstacle.Height:obstacle.Width;
        double left=Math.Max(start,Math.Floor(obstacleAlong-margin)),right=Math.Min(end,Math.Ceiling(obstacleAlong+obstacleAlongSize+margin));
        if(right>left)spans.Add(new TaskbarArea{X=left,Width=right-left});
      }
      spans.Sort(delegate(TaskbarArea a,TaskbarArea b){return a.X.CompareTo(b.X);});
      var gaps=new List<TaskbarArea>();double cursor=start;
      foreach(TaskbarArea span in spans) {if(span.X>cursor)gaps.Add(new TaskbarArea{X=cursor,Width=span.X-cursor});cursor=Math.Max(cursor,span.X+span.Width);}
      if(end>cursor)gaps.Add(new TaskbarArea{X=cursor,Width=end-cursor});
      if(manual) {
        double desired=origin+(length-alongSize)*preferredPosition.Value,bestDistance=Double.PositiveInfinity,best=0;bool found=false;
        foreach(TaskbarArea gap in gaps)if(gap.Width>=alongSize){double candidate=Math.Max(gap.X,Math.Min(gap.X+gap.Width-alongSize,Math.Round(desired))),distance=Math.Abs(candidate-desired);if(distance<bestDistance){bestDistance=distance;best=candidate;found=true;}}
        if(found)return new TaskbarPlacement{Visible=true,Reason="visible",Bounds=new TaskbarArea{X=vertical?cross:best,Y=vertical?best:cross,Width=width,Height=height}};
        return Hidden("no-space");
      }
      if(side=="right")gaps.Reverse();
      foreach(TaskbarArea gap in gaps)if(gap.Width>=alongSize) {
        double along=side=="left"?gap.X:gap.X+gap.Width-alongSize;
        return new TaskbarPlacement{Visible=true,Reason="visible",Bounds=new TaskbarArea{X=vertical?cross:along,Y=vertical?along:cross,Width=width,Height=height}};
      }
      return Hidden("no-space");
    }
  }
}
