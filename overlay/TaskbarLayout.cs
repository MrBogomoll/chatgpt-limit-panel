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
    public static TaskbarPlacement Place(TaskbarSnapshot snapshot,string side) {
      if(snapshot==null)return Hidden("unavailable");
      if(side!="left"&&side!="right")return Hidden("invalid-side");
      if(!snapshot.Visible)return Hidden("taskbar-hidden");
      if(snapshot.AutoHidden)return Hidden("autohide");
      if(snapshot.Fullscreen)return Hidden("fullscreen");
      if(!snapshot.OccupancyKnown||snapshot.Occupied==null)return Hidden("unknown-occupancy");
      if(!Valid(snapshot.Bounds)||!Finite(snapshot.Scale)||snapshot.Scale<=0||snapshot.Scale>8)return Hidden("invalid-geometry");
      TaskbarArea bar=snapshot.Bounds;double scale=snapshot.Scale;
      double width=Math.Ceiling(Width*scale),height=Math.Ceiling(Height*scale),margin=Math.Ceiling(Clearance*scale);
      if(bar.Width<=bar.Height||bar.Height<height)return Hidden("unsupported-taskbar");
      double top=Math.Floor(bar.Y+(bar.Height-height)/2);
      double middle=bar.X+bar.Width/2;
      double start=Math.Ceiling(side=="left"?bar.X+margin:middle+margin);
      double end=Math.Floor(side=="left"?middle-margin:bar.X+bar.Width-margin);
      var spans=new List<TaskbarArea>();
      foreach(TaskbarArea obstacle in snapshot.Occupied) {
        if(!Valid(obstacle))return Hidden("invalid-occupancy");
        if(obstacle.Y>=top+height+margin||obstacle.Y+obstacle.Height<=top-margin)continue;
        double left=Math.Max(start,Math.Floor(obstacle.X-margin)),right=Math.Min(end,Math.Ceiling(obstacle.X+obstacle.Width+margin));
        if(right>left)spans.Add(new TaskbarArea{X=left,Width=right-left});
      }
      spans.Sort(delegate(TaskbarArea a,TaskbarArea b){return a.X.CompareTo(b.X);});
      var gaps=new List<TaskbarArea>();double cursor=start;
      foreach(TaskbarArea span in spans) {if(span.X>cursor)gaps.Add(new TaskbarArea{X=cursor,Width=span.X-cursor});cursor=Math.Max(cursor,span.X+span.Width);}
      if(end>cursor)gaps.Add(new TaskbarArea{X=cursor,Width=end-cursor});
      if(side=="right")gaps.Reverse();
      foreach(TaskbarArea gap in gaps)if(gap.Width>=width) {
        double x=side=="left"?gap.X:gap.X+gap.Width-width;
        return new TaskbarPlacement{Visible=true,Reason="visible",Bounds=new TaskbarArea{X=x,Y=top,Width=width,Height=height}};
      }
      return Hidden("no-space");
    }
  }
}
