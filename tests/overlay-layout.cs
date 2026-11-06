using System;
using QuotaOverlay;
class LayoutTest {
  static void Check(bool ok,string name) { if(!ok) throw new Exception(name); }
  static void Main() { try { Run(); } catch(Exception e) { Console.Error.WriteLine(e.Message);Environment.ExitCode=1; } }
  static void Run() {
    ResetExtension();
    CreditExtensions();
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
      var empty=new System.Collections.Generic.List<RailRegion>();
      foreach(string mode in new[]{"compact","ultra","full"}) {
        var next=Layout.Place(-1800,40,1200*scale,850*scale,scale,mode);
        var position=Layout.PositionForBottom(p.Y+p.Height,40,850*scale,next.Height);
        var placed=Layout.PlaceInRail(next,40,850*scale,scale,empty,position);
        Check(placed.Visible&&Math.Abs(placed.Y+placed.Height-(p.Y+p.Height))<.01&&Math.Abs(placed.X-p.X)<.01,"mode change retains bottom edge and horizontal center");
      }
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
  static void ResetExtension() {
    foreach(double scale in new[]{1.0,1.25,1.5,2.0})foreach(string mode in new[]{"full","compact","ultra"}) {
      double extra=mode=="full"?64:mode=="compact"?36:22;
      var plain=Layout.Place(-1800,40,1200*scale,1000*scale,scale,mode);
      var extended=Layout.Place(-1800,40,1200*scale,1000*scale,scale,mode,true);
      Check(extended.Visible&&extended.Mode==mode,"extended view preserves its mode when space allows");
      Check(Math.Abs(extended.Height-plain.Height-extra*scale)<.01,"reset extension height scales independently in each view");
      Check(Math.Abs(extended.Y+extended.Height-plain.Y-plain.Height)<.01,"turning resets on grows upward without moving the bottom edge");
      Check(Layout.ResetsHeight(mode)==extra&&Layout.HeightForMode(mode,true)==Layout.HeightForMode(mode)+extra,"shared mode sizing includes optional reset extension");
      var floating=Layout.PlaceFloating(-1800,40,500*scale,500*scale,scale,mode,.5,.5,true);
      Check(floating.Visible&&floating.Height==extended.Height&&floating.Y>=40+4*scale&&floating.Y+floating.Height<=40+496*scale,"floating extended views remain inside client bounds at every DPI");
      var occupied=new System.Collections.Generic.List<RailRegion>{new RailRegion{Top=0,Bottom=600*scale},new RailRegion{Top=700*scale,Bottom=1000*scale}};
      var fit=Layout.PlaceInAvailableRail(-1800,40,500*scale,1000*scale,scale,mode,occupied,null,true);
      Check(fit.Visible&&fit.Mode=="ultra"&&fit.Height==54*scale&&fit.Y>=40+604*scale&&fit.Y+fit.Height<=40+696*scale,"only the full extended ultra height may fit a measured gap");
      occupied[1].Top=650*scale;
      Check(!Layout.PlaceInAvailableRail(-1800,40,500*scale,1000*scale,scale,mode,occupied,null,true).Visible,"reset extension cannot overlap controls when even ultra is too tall");
      Check(Layout.PlaceInAvailableRail(-1800,40,500*scale,1000*scale,scale,mode,occupied,null).Visible,"the same gap still fits ordinary ultra with resets disabled");
    }
    Check(Layout.Place(0,0,500,823,1,"full",true).Mode=="compact","reset extension raises the conservative full-mode height boundary");
    Check(Layout.Place(0,0,500,685,1,"compact",true).Mode=="ultra","reset extension raises the conservative compact-mode height boundary");
    Check(!Layout.Place(0,0,500,603,1,"ultra",true).Visible,"reset extension preserves navigation clearance below minimum client height");
    Check(Layout.PlaceFloating(0,0,500,100,1,"full",null,null,true).Mode=="ultra","floating mode falls back using the full extension height");
    Check(!Layout.PlaceFloating(0,0,500,60,1,"ultra",null,null,true).Visible,"floating extension never escapes an undersized client");
  }
  static void CreditExtensions() {
    foreach(double scale in new[]{1.0,1.25,1.5,2.0})foreach(bool resets in new[]{false,true})foreach(bool credits in new[]{false,true})foreach(string mode in new[]{"full","compact","ultra"}) {
      double creditExtra=mode=="full"?40:mode=="compact"?32:22;
      double expected=Layout.HeightForMode(mode,resets)+(credits?creditExtra:0);
      Check(Layout.CreditsHeight(mode)==creditExtra&&Layout.HeightForMode(mode,resets,credits)==expected,"credit and reset extensions add their independent heights");
      var plain=Layout.Place(-1800,40,1200*scale,1000*scale,scale,mode);
      var placed=Layout.Place(-1800,40,1200*scale,1000*scale,scale,mode,resets,credits);
      Check(placed.Visible&&placed.Mode==mode&&Math.Abs(placed.Height-expected*scale)<.01,"all optional-block combinations preserve requested mode and scaled height when space allows");
      Check(Math.Abs(placed.Y+placed.Height-plain.Y-plain.Height)<.01,"optional blocks grow above the original bottom edge");
      var floating=Layout.PlaceFloating(-1800,40,500*scale,500*scale,scale,mode,.5,.5,resets,credits);
      Check(floating.Visible&&floating.Height==placed.Height&&floating.Y>=40+4*scale&&floating.Y+floating.Height<=40+496*scale,"floating geometry bounds include both optional blocks");
      var occupied=new System.Collections.Generic.List<RailRegion>{new RailRegion{Top=0,Bottom=600*scale},new RailRegion{Top=700*scale,Bottom=1000*scale}};
      var fit=Layout.PlaceInAvailableRail(-1800,40,500*scale,1000*scale,scale,mode,occupied,null,resets,credits);
      double ultraHeight=Layout.HeightForMode("ultra",resets,credits);
      Check(fit.Visible&&fit.Mode=="ultra"&&Math.Abs(fit.Height-ultraHeight*scale)<.01&&fit.Y>=40+604*scale&&fit.Y+fit.Height<=40+696*scale,"measured rail fallback includes every visible optional block");
      occupied[1].Top=(600+ultraHeight+7)*scale;
      Check(!Layout.PlaceInAvailableRail(-1800,40,500*scale,1000*scale,scale,mode,occupied,null,resets,credits).Visible,"even ultra cannot overlap rail controls when its total height does not fit");
      double minimum=(mode=="full"?760:mode=="compact"?650:582)+expected-Layout.HeightForMode(mode);
      var boundary=Layout.Place(0,0,500*scale,minimum*scale,scale,mode,resets,credits);
      Check(boundary.Visible&&boundary.Mode==mode,"optional blocks retain exact supported minimum boundary at all scales");
      if(mode!="ultra")Check(Layout.Place(0,0,500*scale,(minimum-1)*scale,scale,mode,resets,credits).Mode!=mode,"optional blocks trigger fallback below the expanded minimum boundary");
      else Check(!Layout.Place(0,0,500*scale,(minimum-1)*scale,scale,mode,resets,credits).Visible,"expanded ultra hides below its minimum boundary");
    }
    Check(Layout.PlaceFloating(0,0,500,100,1,"full",null,null,true,true).Mode=="ultra","floating fallback uses the full combined height");
    Check(!Layout.PlaceFloating(0,0,500,83,1,"ultra",null,null,true,true).Visible,"combined ultra blocks respect the client margin when height is insufficient");
  }
}
