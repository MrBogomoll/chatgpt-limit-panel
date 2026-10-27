using System;

namespace QuotaOverlay {
  internal sealed class TaskbarMenuLease {
    const long EntryLifetimeMilliseconds=1600;
    readonly object gate=new object();
    TaskbarArea footprint;
    string evidence;
    long recordedAt=-1,lastObservedAt=-1;
    bool recorded,active,blockedUntilMenuClose;
    string lastReason="new",lastRejectedReason,lastRecordEvidence;
    long lastRecordAt=-1,lastRejectedAt=-1;
    long? lastRejectedAge;

    internal string LastReason {get {lock(gate)return lastReason;}}
    internal object SnapshotDebug(long now) {
      lock(gate)return new {
        recorded=recorded,active=active,blocked=blockedUntilMenuClose,
        recordedAt=recordedAt,lastObserved=lastObservedAt,
        age=Age(now,recordedAt),reason=lastReason,evidenceHash=Hash(evidence),
        lastRecordAt=lastRecordAt,lastRecordEvidenceHash=Hash(lastRecordEvidence),
        lastRejectedAt=lastRejectedAt,lastRejectedAge=lastRejectedAge,lastRejectedReason=lastRejectedReason
      };
    }

    // This records an already displayed, verified position. It neither leases a
    // whole occupancy snapshot nor changes its measurement time in the reader.
    internal void Record(TaskbarSnapshot snapshot,TaskbarPlacement shown,string fingerprint,long now) {
      lock(gate) {
        if(blockedUntilMenuClose)return;
        if(now<0){Reject("clock-negative",active,now);return;}
        if(now<lastObservedAt){Reject("clock-backwards",active,now);return;}
        if(String.IsNullOrWhiteSpace(fingerprint)){Reject("missing-evidence",active,now);return;}
        if(active) {
          // Fresh accessibility evidence may revoke a lease, but cannot move it.
          if(!String.Equals(evidence,fingerprint,StringComparison.Ordinal)){Reject("evidence-changed",true,now);return;}
          string fitReason=FitReason(snapshot,footprint);
          if(fitReason!=null){Reject("fresh-footprint-contradiction/"+fitReason,true,now);return;}
          lastObservedAt=now;lastReason="fresh-confirmed";return;
        }
        if(shown==null){Reject("placement-missing",false,now);return;}
        if(!shown.Visible){Reject("placement-hidden",false,now);return;}
        string initialFitReason=FitReason(snapshot,shown.Bounds);
        if(initialFitReason!=null){Reject("invalid-footprint/"+initialFitReason,false,now);return;}
        footprint=shown.Bounds;evidence=fingerprint;recordedAt=lastObservedAt=now;recorded=true;
        lastRecordAt=now;lastRecordEvidence=fingerprint;lastReason="recorded";
      }
    }
    internal bool TryHold(bool menuActive,bool alreadyVisible,bool nativeSafe,string fingerprint,long now,out TaskbarPlacement placement) {
      lock(gate) {
        placement=null;
        if(!menuActive){Clear(false);lastReason="normal-mode";return false;}
        if(blockedUntilMenuClose)return false;
        if(!recorded){Reject("not-recorded",true,now);return false;}
        if(!alreadyVisible){Reject("not-visible",true,now);return false;}
        if(!nativeSafe){Reject("native-unsafe",true,now);return false;}
        if(now<0){Reject("clock-negative",true,now);return false;}
        if(now<lastObservedAt){Reject("clock-backwards",true,now);return false;}
        if(!String.Equals(evidence,fingerprint,StringComparison.Ordinal)){Reject("evidence-changed",true,now);return false;}
        if(!active&&now-recordedAt>EntryLifetimeMilliseconds){Reject("entry-expired",true,now);return false;}
        active=true;lastObservedAt=now;lastReason="held";
        placement=new TaskbarPlacement{Visible=true,Reason="menu-held",Bounds=footprint};return true;
      }
    }
    internal void Invalidate() {lock(gate){Clear(false);lastReason="invalidated";}}
    void Reject(string reason,bool blocked,long now) {
      lastRejectedAt=now;lastRejectedAge=Age(now,recordedAt);lastRejectedReason=reason;
      Clear(blocked);lastReason=reason;
    }
    static long? Age(long now,long then) {return now>=0&&then>=0&&now>=then?(long?)(now-then):null;}
    static string Hash(string value) {
      if(value==null)return null;
      unchecked {uint hash=2166136261;foreach(char item in value){hash^=item;hash*=16777619;}return hash.ToString("X8");}
    }
    void Clear(bool blocked) {
      recorded=false;active=false;blockedUntilMenuClose=blocked;evidence=null;
      recordedAt=lastObservedAt=-1;footprint=new TaskbarArea();
    }
    static string FitReason(TaskbarSnapshot snapshot,TaskbarArea bounds) {
      if(snapshot==null)return "snapshot-missing";
      if(snapshot.Handle==IntPtr.Zero)return "taskbar-missing";
      if(!snapshot.Visible)return "taskbar-hidden";
      if(!snapshot.OccupancyKnown)return "occupancy-unknown";
      if(snapshot.AutoHidden)return "autohide";
      if(snapshot.Fullscreen)return "fullscreen";
      if(snapshot.Occupied==null)return "occupancy-missing";
      if(!TaskbarLayout.Valid(snapshot.Bounds))return "taskbar-geometry";
      if(!TaskbarLayout.Valid(bounds))return "footprint-geometry";
      if(!TaskbarLayout.Finite(snapshot.Scale)||snapshot.Scale<=0||snapshot.Scale>8)return "scale";
      if(bounds.Width!=Math.Ceiling(TaskbarLayout.Width*snapshot.Scale)||bounds.Height!=Math.Ceiling(TaskbarLayout.Height*snapshot.Scale))return "footprint-size";
      TaskbarArea bar=snapshot.Bounds;
      if(bounds.X<bar.X||bounds.Y<bar.Y||bounds.X+bounds.Width>bar.X+bar.Width||bounds.Y+bounds.Height>bar.Y+bar.Height)return "outside-taskbar";
      double margin=Math.Ceiling(TaskbarLayout.Clearance*snapshot.Scale);
      foreach(TaskbarArea obstacle in snapshot.Occupied) {
        if(!TaskbarLayout.Valid(obstacle))return "obstacle-geometry";
        if(obstacle.X<bounds.X+bounds.Width+margin&&obstacle.X+obstacle.Width>bounds.X-margin&&obstacle.Y<bounds.Y+bounds.Height+margin&&obstacle.Y+obstacle.Height>bounds.Y-margin)return "obstacle-overlap";
      }
      return null;
    }
  }
}
