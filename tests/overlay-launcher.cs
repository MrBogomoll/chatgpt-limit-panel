using System;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

class OverlayLauncherTest {
  static void Check(bool value,string message) { if(!value)throw new Exception(message); }
  [STAThread] public static int Main(string[] args) {
    try {
      if(args[0]=="--pipe") {
        QuotaMonitorLauncher.StartDetachedHost(args[2],Path.Combine(args[1],"background.cjs"),Path.Combine(args[1],"child.json"),"unused",true);
        Console.WriteLine("PASS detached launcher closes its caller pipes");return 0;
      }
      string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
      string native=Path.Combine(root,"native");Directory.CreateDirectory(native);
      Check(QuotaMonitorLauncher.ResolveRuntimePath(native,"../runtime/node.exe")==Path.Combine(root,"runtime","node.exe"),"portable runtime resolves relative to launcher, not working directory");
      string bins=Path.Combine(root,"OpenAI","Codex","bin");
      string older=Path.Combine(bins,"older","codex.exe"),newer=Path.Combine(bins,"newer","codex.exe");
      Directory.CreateDirectory(Path.GetDirectoryName(older));Directory.CreateDirectory(Path.GetDirectoryName(newer));
      File.WriteAllText(older,"fixture");File.WriteAllText(newer,"fixture");
      File.SetLastWriteTimeUtc(older,new DateTime(2025,1,1));File.SetLastWriteTimeUtc(newer,new DateTime(2026,1,1));
      string oldOverride=Environment.GetEnvironmentVariable("QUOTA_CODEX_BIN");
      string oldCli=Environment.GetEnvironmentVariable("CODEX_CLI_PATH");
      try {
        Environment.SetEnvironmentVariable("QUOTA_CODEX_BIN",null);Environment.SetEnvironmentVariable("CODEX_CLI_PATH",null);
        Check(QuotaMonitorLauncher.DiscoverCodex(root,"auto",native)==newer,"portable launcher discovers newest installed native Codex");
        Check(QuotaMonitorLauncher.DiscoverCodex(root,older,native)==older,"explicit valid runtime configuration is respected");
        Environment.SetEnvironmentVariable("QUOTA_CODEX_BIN",older);
        Check(QuotaMonitorLauncher.DiscoverCodex(root,"auto",native)==older,"per-machine environment override is respected");
        Environment.SetEnvironmentVariable("QUOTA_CODEX_BIN",Path.Combine(root,"missing.exe"));
        Check(QuotaMonitorLauncher.DiscoverCodex(root,"auto",native)==newer,"stale versioned override falls back to discovery");
      } finally {Environment.SetEnvironmentVariable("QUOTA_CODEX_BIN",oldOverride);Environment.SetEnvironmentVariable("CODEX_CLI_PATH",oldCli);}
      string name="Local\\QuotaLauncherTest."+Guid.NewGuid().ToString("N");
      Check(!QuotaMonitorLauncher.ReadStatus(root,name).running,"missing own mutex reports stopped despite any state file");
      using(var mutex=new Mutex(false,name)) {
        File.WriteAllText(Path.Combine(root,"state.json"),"{\"process\":99999999,\"visible\":true,\"paused\":true}");
        var status=QuotaMonitorLauncher.ReadStatus(root,name);
        Check(status.running && status.status=="running","live own mutex reports running");
        Check(!status.visible && !status.paused,"dead saved process cannot fabricate a visible or paused monitor");
        Check(new JavaScriptSerializer().Serialize(status).Contains("running"),"native result serializes to machine-readable JSON");
      }
      Check(!QuotaMonitorLauncher.ReadStatus(root,name).running,"released mutex reports stopped");
      bool newMonitor=false;int startAttempts=0;
      QuotaMonitorLauncher.StartAndWait(()=>newMonitor,()=>{if(++startAttempts==3)newMonitor=true;},1500);
      Check(newMonitor && startAttempts==3,"start retries while a previous scheduler host finishes stopping");
      QuotaMonitorLauncher.StartAndWait(()=>true,()=>{throw new Exception("must not restart a live monitor");},100);
      bool stoppingMonitor=true;int stopAttempts=0;
      QuotaMonitorLauncher.StopAndWait(()=>stoppingMonitor,()=>{if(++stopAttempts==3)stoppingMonitor=false;},1500);
      Check(!stoppingMonitor && stopAttempts==3,"stop retries while a newly started GUI prepares its cooperative stop event");
      QuotaMonitorLauncher.StopAndWait(()=>false,()=>{throw new Exception("must not signal a stopped monitor");},100);
      Console.WriteLine("PASS launcher portability, native Codex discovery, stale overrides and own mutex status");return 0;
    } catch(Exception e) {Console.Error.WriteLine(e.Message);return 1;}
  }
}
