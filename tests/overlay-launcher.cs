using System;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

class OverlayLauncherTest {
  static void Check(bool value,string message) { if(!value)throw new Exception(message); }
  [STAThread] public static int Main(string[] args) {
    try {
      if(args[0]=="--pipe") {
        LimitPanelLauncher.StartDetachedHost(args[2],Path.Combine(args[1],"background.cjs"),Path.Combine(args[1],"child.json"),"unused",true);
        Console.WriteLine("PASS detached launcher closes its caller pipes");return 0;
      }
      string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
      string native=Path.Combine(root,"native");Directory.CreateDirectory(native);
      Check(LimitPanelLauncher.ResolveRuntimePath(native,"../runtime/node.exe")==Path.Combine(root,"runtime","node.exe"),"portable runtime resolves relative to launcher, not working directory");
      string bins=Path.Combine(root,"OpenAI","Codex","bin");
      string older=Path.Combine(bins,"older","codex.exe"),newer=Path.Combine(bins,"newer","codex.exe");
      Directory.CreateDirectory(Path.GetDirectoryName(older));Directory.CreateDirectory(Path.GetDirectoryName(newer));
      File.WriteAllText(older,"fixture");File.WriteAllText(newer,"fixture");
      File.SetLastWriteTimeUtc(older,new DateTime(2025,1,1));File.SetLastWriteTimeUtc(newer,new DateTime(2026,1,1));
      string oldOverride=Environment.GetEnvironmentVariable("QUOTA_CODEX_BIN");
      string oldCli=Environment.GetEnvironmentVariable("CODEX_CLI_PATH");
      try {
        Environment.SetEnvironmentVariable("QUOTA_CODEX_BIN",null);Environment.SetEnvironmentVariable("CODEX_CLI_PATH",null);
        Check(LimitPanelLauncher.DiscoverCodex(root,"auto",native)==newer,"portable launcher discovers newest installed native Codex");
        Check(LimitPanelLauncher.DiscoverCodex(root,older,native)==older,"explicit valid runtime configuration is respected");
        Environment.SetEnvironmentVariable("QUOTA_CODEX_BIN",older);
        Check(LimitPanelLauncher.DiscoverCodex(root,"auto",native)==older,"per-machine environment override is respected");
        Environment.SetEnvironmentVariable("QUOTA_CODEX_BIN",Path.Combine(root,"missing.exe"));
        Check(LimitPanelLauncher.DiscoverCodex(root,"auto",native)==newer,"stale versioned override falls back to discovery");
      } finally {Environment.SetEnvironmentVariable("QUOTA_CODEX_BIN",oldOverride);Environment.SetEnvironmentVariable("CODEX_CLI_PATH",oldCli);}
      string name="Local\\QuotaLauncherTest."+Guid.NewGuid().ToString("N");
      Check(!LimitPanelLauncher.ReadStatus(root,name).running,"missing own mutex reports stopped despite any state file");
      using(var mutex=new Mutex(false,name)) {
        File.WriteAllText(Path.Combine(root,"state.json"),"{\"process\":99999999,\"visible\":true,\"paused\":true}");
        var status=LimitPanelLauncher.ReadStatus(root,name);
        Check(status.running && status.status=="running","live own mutex reports running");
        Check(!status.visible && !status.paused,"dead saved process cannot fabricate a visible or paused monitor");
        Check(new JavaScriptSerializer().Serialize(status).Contains("running"),"native result serializes to machine-readable JSON");
      }
      Check(!LimitPanelLauncher.ReadStatus(root,name).running,"released mutex reports stopped");
      File.WriteAllText(Path.Combine(root,"settings.json"),"{\"language\":\"en\",\"themeMode\":\"light\",\"autoStartEnabled\":false}");
      var english=LimitPanelLauncher.ReadStatus(root,name);
      Check(english.language=="en"&&english.message=="Panel is stopped.","launcher must pass the saved language and translated status into the card");
      Check(File.ReadAllText(Path.Combine(root,"settings.json")).Contains("false"),"status must not alter language, theme or autostart settings");
      File.WriteAllText(Path.Combine(root,"settings.json"),"{\"language\":false}");
      Check(LimitPanelLauncher.ReadStatus(root,name).language=="ru","invalid native language must safely fall back to Russian");
      bool newMonitor=false;int startAttempts=0;
      LimitPanelLauncher.StartAndWait(()=>newMonitor,()=>{if(++startAttempts==3)newMonitor=true;},1500);
      Check(newMonitor && startAttempts==3,"start retries while a previous scheduler host finishes stopping");
      LimitPanelLauncher.StartAndWait(()=>true,()=>{throw new Exception("must not restart a live monitor");},100);
      bool stoppingMonitor=true;int stopAttempts=0;
      LimitPanelLauncher.StopAndWait(()=>stoppingMonitor,()=>{if(++stopAttempts==3)stoppingMonitor=false;},1500);
      Check(!stoppingMonitor && stopAttempts==3,"stop retries while a newly started GUI prepares its cooperative stop event");
      LimitPanelLauncher.StopAndWait(()=>false,()=>{throw new Exception("must not signal a stopped monitor");},100);
      Console.WriteLine("PASS launcher portability, native Codex discovery, stale overrides and own mutex status");return 0;
    } catch(Exception e) {Console.Error.WriteLine(e.Message);return 1;}
  }
}
