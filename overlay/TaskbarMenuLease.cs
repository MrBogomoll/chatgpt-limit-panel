using System;

namespace QuotaOverlay {
  internal sealed class TaskbarMenuLease {
    const long EntryLifetimeMilliseconds=1600;
    readonly object gate=new object();
    TaskbarArea footprint;
    string evidence;
    long recordedAt=-1,lastObservedAt=-1;
    bool recorded,active,blockedUntilMenuClose;

    // This records an already displayed, verified position. It neither leases a
    // whole occupancy snapshot nor changes its measurement time in the reader.
    internal void Record(TaskbarSnapshot snapshot,TaskbarPlacement shown,string fingerprint,long now) {
      lock(gate) {
        if(blockedUntilMenuClose)return;
        if(now<0||now<lastObservedAt||String.IsNullOrWhiteSpace(fingerprint)) {Clear(active);return;}
        if(active) {
          // Fresh accessibility evidence may revoke a lease, but cannot move it.
          if(!String.Equals(evidence,fingerprint,StringComparison.Ordinal)||!Fits(snapshot,footprint)){Clear(true);return;}
          lastObservedAt=now;return;
        }
        if(shown==null||!shown.Visible||!Fits(snapshot,shown.Bounds)){Clear(false);return;}
        footprint=shown.Bounds;evidence=fingerprint;recordedAt=lastObservedAt=now;recorded=true;
      }
    }
    internal bool TryHold(bool menuActive,bool alreadyVisible,bool nativeSafe,string fingerprint,long now,out TaskbarPlacement placement) {
      lock(gate) {
        placement=null;
        if(!menuActive){Clear(false);return false;}
        if(blockedUntilMenuClose)return false;
        if(!recorded||!alreadyVisible||!nativeSafe||now<0||now<lastObservedAt||!String.Equals(evidence,fingerprint,StringComparison.Ordinal)) {Clear(true);return false;}
        if(!active&&now-recordedAt>EntryLifetimeMilliseconds){Clear(true);return false;}
        active=true;lastObservedAt=now;
        placement=new TaskbarPlacement{Visible=true,Reason="menu-held",Bounds=footprint};return true;
      }
    }
    internal void Invalidate() {lock(gate)Clear(false);}
    void Clear(bool blocked) {
      recorded=false;active=false;blockedUntilMenuClose=blocked;evidence=null;
      recordedAt=lastObservedAt=-1;footprint=new TaskbarArea();
    }
    static bool Fits(TaskbarSnapshot snapshot,TaskbarArea bounds) {
      if(snapshot==null||snapshot.Handle==IntPtr.Zero||!snapshot.Visible||!snapshot.OccupancyKnown||snapshot.AutoHidden||snapshot.Fullscreen||snapshot.Occupied==null)return false;
      if(!TaskbarLayout.Valid(snapshot.Bounds)||!TaskbarLayout.Valid(bounds)||!TaskbarLayout.Finite(snapshot.Scale)||snapshot.Scale<=0||snapshot.Scale>8)return false;
      if(bounds.Width!=Math.Ceiling(TaskbarLayout.Width*snapshot.Scale)||bounds.Height!=Math.Ceiling(TaskbarLayout.Height*snapshot.Scale))return false;
      TaskbarArea bar=snapshot.Bounds;
      if(bounds.X<bar.X||bounds.Y<bar.Y||bounds.X+bounds.Width>bar.X+bar.Width||bounds.Y+bounds.Height>bar.Y+bar.Height)return false;
      double margin=Math.Ceiling(TaskbarLayout.Clearance*snapshot.Scale);
      foreach(TaskbarArea obstacle in snapshot.Occupied) {
        if(!TaskbarLayout.Valid(obstacle))return false;
        if(obstacle.X<bounds.X+bounds.Width+margin&&obstacle.X+obstacle.Width>bounds.X-margin&&obstacle.Y<bounds.Y+bounds.Height+margin&&obstacle.Y+obstacle.Height>bounds.Y-margin)return false;
      }
      return true;
    }
  }
}
