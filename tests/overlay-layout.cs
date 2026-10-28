using System;
using QuotaOverlay;
class LayoutTest {
  static void Check(bool ok,string name) { if(!ok) throw new Exception(name); }
  static void Main() { try { Run(); } catch(Exception e) { Console.Error.WriteLine(e.Message);Environment.ExitCode=1; } }
  static void Run() {
    foreach(double scale in new double[]{1,1.25,1.5,2}) {
      foreach(string choice in new[]{"full","compact"}) {
        var automaticUltra=Layout.Place(-1800,40,500*scale,600*scale,scale,choice);
        Check(automaticUltra.Visible&&automaticUltra.Mode=="ultra"&&automaticUltra.Height==32*scale,"further window reduction must automatically reach ultracompact at every DPI");
        var restored=Layout.Place(-1800,40,500*scale,850*scale,scale,choice);
        Check(restored.Mode==choice,"growing the window restores the preferred view");
        var narrowGap=new System.Collections.Generic.List<RailRegion>{new RailRegion{Top=0,Bottom=600*scale},new RailRegion{Top=650*scale,Bottom=850*scale}};
        var fit=Layout.PlaceInAvailableRail(-1800,40,500*scale,850*scale,scale,choice,narrowGap,null);
        Check(fit.Visible&&fit.Mode=="ultra"&&fit.Y>=40+604*scale&&fit.Y+fit.Height<=40+646*scale,"only ultracompact fits a measured short gap without covering controls");
        var shortRail=new System.Collections.Generic.List<RailRegion>{new RailRegion{Top=0,Bottom=470*scale},new RailRegion{Top=530*scale,Bottom=570*scale}};
        fit=Layout.PlaceInAvailableRail(-1800,40,500*scale,570*scale,scale,choice,shortRail,null);
        Check(fit.Visible&&fit.Mode=="ultra"&&fit.Y>=40+474*scale&&fit.Y+fit.Height<=40+526*scale,"measured short windows can use ultra below the conservative unknown-geometry cutoff");
        Check(!Layout.PlaceInAvailableRail(0,0,500*scale,570*scale,scale,choice,null,null).Visible,"unknown geometry cannot authorize any measured fallback");
      }
      var p=Layout.Place(-1800,40,1200*scale,850*scale,scale);
      Check(p.Visible,"visible at supported DPI");
      Check(p.X>=-1800 && p.X+p.Width<=-1800+52*scale,"stays in rail at negative monitor coordinates");
      Check(p.Y+p.Height<=40+(850-110)*scale,"leaves update button and avatar free");
      Check(Math.Abs(p.Width-46*scale)<1,"width scales");
      Check(Math.Abs(p.Height-192*scale)<0.01,"full height including bars scales");
      Check(!p.Compact,"full mode preferred when space allows");
      var controls=new System.Collections.Generic.List<RailRegion>{new RailRegion{Top=0,Bottom=450*scale},new RailRegion{Top=712*scale,Bottom=748*scale},new RailRegion{Top=806*scale,Bottom=850*scale}};
      var safe=Layout.PlaceInRail(p,40,850*scale,scale,controls,null);
      Check(safe.Visible&&safe.Y+safe.Height<=40+708*scale,"new native quota button moves our panel into the nearest actual free gap");
      safe=Layout.PlaceInRail(p,40,850*scale,scale,controls,0.05);
      Check(safe.Visible&&safe.Y>=40+454*scale,"manual rail placement cannot cross navigation buttons");
      controls[1]=new RailRegion{Top=780*scale,Bottom=806*scale};safe=Layout.PlaceInRail(p,40,850*scale,scale,controls,null);
      Check(safe.Visible&&Math.Abs(safe.Y-p.Y)<.01,"updated control positions restore the default slot without changing their count");
      var blocked=new System.Collections.Generic.List<RailRegion>{new RailRegion{Top=0,Bottom=850*scale}};
      Check(!Layout.PlaceInRail(p,40,850*scale,scale,blocked,null).Visible,"occupied rail never permits overlapping placement");
      Check(!Layout.PlaceInRail(p,40,850*scale,scale,null,null).Visible,"unknown rail geometry never guesses an upward slot that could cover navigation");
      var compact=Layout.Place(-1800,40,500*scale,680*scale,scale);
      Check(compact.Visible,"680 DIP client uses compact fallback at supported DPI");
      Check(compact.Compact,"fallback reports compact mode");
      Check(Math.Abs(compact.Height-100*scale)<0.01,"compact height scales");
      Check(Math.Abs(compact.Y+compact.Height-(40+(680-110)*scale))<0.01,"compact leaves update button and avatar free");
      Check(compact.X>=-1800 && compact.X+compact.Width<=-1800+52*scale,"compact stays in rail at negative monitor coordinates");
      var preferred=Layout.Place(-1800,40,500*scale,850*scale,scale,true);
      Check(preferred.Visible && preferred.Compact,"explicit compact preference is honored on tall windows");
      Check(Math.Abs(preferred.Height-100*scale)<0.01,"preferred compact height scales");
      Check(Math.Abs(preferred.Y+preferred.Height-(40+(850-110)*scale))<0.01,"preferred compact keeps bottom clearance");
      var ultra=Layout.Place(-1800,40,500*scale,600*scale,scale,"ultra");
      Check(ultra.Visible && ultra.Compact && ultra.Mode=="ultra" && Math.Abs(ultra.Height-32*scale)<.01,"explicit ultra must use a 32 DIP frame on shorter windows");
      Check(Math.Abs(ultra.Y+ultra.Height-(40+(600-110)*scale))<.01,"ultra must retain bottom clearance at every DPI");
      Check(!Layout.Place(0,0,500*scale,581*scale,scale,"ultra").Visible,"ultra must hide below its navigation clearance boundary");
      Check(Layout.Place(0,0,320*scale,760*scale,scale,false).Visible,"full minimum width and height remain visible");
      Check(!Layout.Place(0,0,320*scale,760*scale,scale,false).Compact,"760 DIP selects full mode");
      Check(Layout.Place(0,0,320*scale,759*scale,scale,false).Compact,"below full minimum selects compact mode");
      Check(Layout.Place(0,0,320*scale,650*scale,scale,false).Visible,"compact minimum remains visible");
      Check(Layout.Place(0,0,320*scale,650*scale,scale,false).Compact,"650 DIP selects compact mode");
      Check(Layout.Place(0,0,500*scale,649*scale,scale,false).Visible&&Layout.Place(0,0,500*scale,649*scale,scale,false).Mode=="ultra","below compact minimum selects automatic ultra");
      Check(Layout.Place(0,0,500*scale,649*scale,scale,true).Visible&&Layout.Place(0,0,500*scale,649*scale,scale,true).Mode=="ultra","compact preference permits smaller effective ultra");
      Check(!Layout.Place(0,0,319*scale,850*scale,scale,true).Visible,"below minimum width hides compact mode");
      Check(!Layout.Place(0,0,319*scale,850*scale,scale,false).Visible,"below minimum width hides full mode");
    }
    Check(!Layout.Place(0,0,500,550,1).Visible,"short window hides instead of covering navigation");
    Check(Layout.Place(0,0,500,720,1).Visible,"720 DIP client uses compact fallback");
    Check(Layout.Place(0,0,500,778,1).Visible,"raised panel still fits the tested narrow window");
    Check(!Layout.Place(0,0,100,800,1).Visible,"tiny window hides");
    Console.WriteLine("PASS overlay geometry: full and compact modes, preferences, DPI, boundaries, update button clearance");
  }
}
