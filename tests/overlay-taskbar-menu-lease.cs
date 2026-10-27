using System;
using QuotaOverlay;

internal static class TaskbarMenuLeaseTest {
  static int assertions;
  static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
  static TaskbarArea Area(double x,double y,double width,double height){return new TaskbarArea{X=x,Y=y,Width=width,Height=height};}
  static TaskbarSnapshot Snapshot(){var s=new TaskbarSnapshot{Handle=new IntPtr(123),Bounds=Area(0,1000,1920,48),Scale=1,Visible=true,OccupancyKnown=true};s.Occupied.Add(Area(0,1000,800,48));s.Occupied.Add(Area(1680,1000,240,48));return s;}
  static TaskbarPlacement Shown(TaskbarSnapshot s){var p=TaskbarLayout.Place(s,"right");Check(p.Visible,"fixture has a real allocated safe gap");return p;}
  static bool Same(TaskbarArea a,TaskbarArea b){return a.X==b.X&&a.Y==b.Y&&a.Width==b.Width&&a.Height==b.Height;}
  static bool Hold(TaskbarMenuLease lease,long now,out TaskbarPlacement placement){return lease.TryHold(true,true,true,"stable",now,out placement);}
  static void StableMenu(){
    var lease=new TaskbarMenuLease();var s=Snapshot();var shown=Shown(s);TaskbarArea expected=shown.Bounds;lease.Record(s,shown,"stable",1000);
    TaskbarPlacement held;
    Check(Hold(lease,1100,out held),"recognized menu must retain the already visible verified footprint");
    Check(held.Visible&&held.Reason=="menu-held"&&Same(held.Bounds,expected),"held placement is the exact original footprint");
    shown.Bounds=Area(1200,1008,48,32);s.Bounds=Area(9000,9000,1,1);s.Occupied.Clear();
    Check(Hold(lease,2800,out held)&&Same(held.Bounds,expected),"a blocked refresh beyond the 1600ms snapshot TTL must not move the menu-held footprint");
    held.Bounds=Area(4,4,4,4);
    Check(Hold(lease,16000,out held)&&Same(held.Bounds,expected),"fifteen seconds of matching fresh native guards retain a copied footprint");
    Check(Hold(lease,100000,out held)&&Same(held.Bounds,expected),"native validation may maintain one active menu session without renewing occupancy snapshots");
    Check(!lease.TryHold(false,true,true,"stable",100001,out held)&&held==null,"menu close revokes the lease until a fresh normal placement");
    Check(!Hold(lease,100002,out held),"a new menu cannot reuse the ended menu's position");
  }
  static void EntryGuards(){
    TaskbarPlacement held;var lease=new TaskbarMenuLease();
    Check(!Hold(lease,100,out held)&&held==null,"an unknown initial position cannot create a visible widget");
    foreach(long now in new[]{999L,2601L,Int64.MaxValue}){lease=new TaskbarMenuLease();var s=Snapshot();lease.Record(s,Shown(s),"stable",1000);Check(!Hold(lease,now,out held),"a rollback or expired unstarted record must not become a menu lease");}
    lease=new TaskbarMenuLease();var fresh=Snapshot();lease.Record(fresh,Shown(fresh),"stable",1000);Check(Hold(lease,2600,out held),"the exact accepted 1600ms entry boundary is valid");
    foreach(string key in new[]{null,"","changed","STABLE"}){lease=new TaskbarMenuLease();var s=Snapshot();lease.Record(s,Shown(s),"stable",1000);Check(!lease.TryHold(true,true,true,key,1001,out held),"missing or changed current native evidence prevents entry");}
    lease=new TaskbarMenuLease();fresh=Snapshot();lease.Record(fresh,Shown(fresh),"stable",1000);Check(!lease.TryHold(true,false,true,"stable",1001,out held),"a hidden widget cannot be revived by a menu lease");
    Check(!Hold(lease,1002,out held),"a hidden-state failure cannot later restart the same lease");
  }
  static void RevokeOnChanges(){
    foreach(int change in new[]{0,1,2,3}){
      var lease=new TaskbarMenuLease();var s=Snapshot();var shown=Shown(s);lease.Record(s,shown,"stable",1000);TaskbarPlacement held;Check(Hold(lease,1100,out held),"fixture enters active menu lease");
      bool visible=change!=0,safe=change!=1;string key=change==2?"native-layout-changed":"stable";long time=change==3?999:5000;
      Check(!lease.TryHold(true,visible,safe,key,time,out held)&&held==null,"visibility, native geometry/fingerprint and clock failures revoke immediately");
      lease.Record(s,shown,"stable",5001);Check(!Hold(lease,5002,out held),"failed native guards latch until the menu closes");
      lease.TryHold(false,true,true,"stable",5003,out held);lease.Record(s,shown,"stable",5004);Check(Hold(lease,5005,out held),"a fresh normal record can rearm after menu close");
    }
    var invalidated=new TaskbarMenuLease();var f=Snapshot();invalidated.Record(f,Shown(f),"stable",10);TaskbarPlacement p;Check(Hold(invalidated,11,out p),"explicit invalidation fixture enters");invalidated.Invalidate();Check(!Hold(invalidated,12,out p),"disable, pause, side change and disposal may explicitly invalidate");
  }
  static void RejectUnsafeRecords(){
    foreach(int change in new[]{0,1,2,3,4,5,6,7,8,9}){
      var lease=new TaskbarMenuLease();var s=Snapshot();var p=Shown(s);string key="stable";long now=1000;
      if(change==0)s.OccupancyKnown=false;if(change==1)s.Visible=false;if(change==2)s.Fullscreen=true;if(change==3)s.AutoHidden=true;if(change==4)s.Handle=IntPtr.Zero;
      if(change==5)p.Visible=false;if(change==6)p.Bounds=Area(1800,1008,48,32);if(change==7)p.Bounds=Area(-1,1008,48,32);if(change==8)key=" ";if(change==9)now=-1;
      lease.Record(s,p,key,now);TaskbarPlacement held;Check(!Hold(lease,1001,out held),"unsafe snapshots and unverified footprints cannot be recorded");
    }
  }
  static void FreshContradiction(){
    var lease=new TaskbarMenuLease();var s=Snapshot();var shown=Shown(s);TaskbarArea original=shown.Bounds;lease.Record(s,shown,"stable",1000);TaskbarPlacement held;Check(Hold(lease,1001,out held),"fresh contradiction fixture starts");
    var alternate=new TaskbarPlacement{Visible=true,Bounds=Area(1500,1008,48,32)};lease.Record(s,alternate,"stable",1100);Check(Hold(lease,17000,out held)&&Same(held.Bounds,original),"a record cannot move an already active menu lease");
    s.Occupied.Add(original);lease.Record(s,alternate,"stable",17001);Check(!Hold(lease,17002,out held),"fresh accessibility evidence contradicting the held footprint revokes it");
  }
  static int Main(){try{StableMenu();EntryGuards();RevokeOnChanges();RejectUnsafeRecords();FreshContradiction();Console.WriteLine("PASS fixed-footprint menu lease ("+assertions+" assertions)");return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
