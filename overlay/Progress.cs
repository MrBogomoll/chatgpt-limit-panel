using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace QuotaOverlay {
  public static class ProgressGeometry {
    // A partial rounded rectangle measured along its perimeter, starting at 12 o'clock.
    // Sampling the four corners avoids angle-based progress on a tall rectangle.
    public static PathGeometry Create(double width,double height,double fraction) {
      var result=new PathGeometry();
      if(Double.IsNaN(fraction)||Double.IsInfinity(fraction)||fraction<=0||width<20||height<20)return result;
      fraction=Math.Min(1,fraction);
      const double inset=1.25,radius=7.75;
      double left=inset,top=inset,right=width-inset,bottom=height-inset;
      var points=new List<Point> {new Point(width/2,top),new Point(right-radius,top)};
      Corner(points,right-radius,top+radius,radius,-Math.PI/2);
      points.Add(new Point(right,bottom-radius));Corner(points,right-radius,bottom-radius,radius,0);
      points.Add(new Point(left+radius,bottom));Corner(points,left+radius,bottom-radius,radius,Math.PI/2);
      points.Add(new Point(left,top+radius));Corner(points,left+radius,top+radius,radius,Math.PI);
      points.Add(new Point(width/2,top));
      double length=0;for(int i=1;i<points.Count;i++)length+=(points[i]-points[i-1]).Length;
      double remaining=length*fraction;
      var figure=new PathFigure {StartPoint=points[0],IsFilled=false,IsClosed=fraction==1};
      for(int i=1;i<points.Count && remaining>0;i++) {
        Vector segment=points[i]-points[i-1];double distance=segment.Length;
        Point end=remaining>=distance?points[i]:points[i-1]+segment*(remaining/distance);
        figure.Segments.Add(new LineSegment(end,true));remaining-=distance;
      }
      result.Figures.Add(figure);result.Freeze();return result;
    }
    static void Corner(List<Point> points,double x,double y,double radius,double start) {
      for(int step=1;step<=16;step++) {
        double angle=start+Math.PI/2*step/16;
        points.Add(new Point(x+radius*Math.Cos(angle),y+radius*Math.Sin(angle)));
      }
    }
  }
}
