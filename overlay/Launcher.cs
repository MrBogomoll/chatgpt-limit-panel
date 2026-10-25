using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class QuotaMonitorLauncher {
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct StartupInfo {
    public int cb;public string reserved,desktop,title;public int x,y,xSize,ySize,xCountChars,yCountChars,fillAttribute,flags;public short showWindow,reservedSize;public IntPtr reservedBytes,input,output,error;
  }
  [StructLayout(LayoutKind.Sequential)] struct ProcessInfo {public IntPtr process,thread;public uint processId,threadId;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateProcess(string application,StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inheritHandles,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo process);
  [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
  [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr process,out uint code);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
  const string MonitorMutex="Local\\ChatGPTQuotaOverlay.v1",StopEvent="Local\\ChatGPTQuotaOverlay.Stop";
  internal class MonitorStatus {
    public string status,message,language;
    public bool running,changed,visible,paused;
    public bool autoStartEnabled,autoStartConfigured;
  }
  [STAThread] static void Main(string[] args) {
    bool jsonMode=args.Length==1 && args[0].EndsWith("-json",StringComparison.Ordinal);
    bool taskHostMode=args.Length==1 && args[0]=="--run-host";
    try {
      string dir=AppDomain.CurrentDomain.BaseDirectory;
      string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChatGPTQuotaOverlay");Directory.CreateDirectory(data);
      string command=args.Length==0?"--start":args[0];
      if(args.Length>1 || !new[]{"--start","--stop","--status-json","--start-json","--stop-json","--run-host","--enable-autostart-json","--disable-autostart-json"}.Contains(command))throw new Exception(QuotaOverlay.PanelLanguage.Text(QuotaOverlay.PanelLanguage.Current,"unknownCommand"));
      var before=ReadStatus(data,MonitorMutex);
      string launcher=Path.Combine(dir,"QuotaMonitor.exe");
      if(command=="--status-json") {FillAutostart(before,launcher);WriteJson(before);return;}
      if(taskHostMode) {
        if(!before.running)StartConfiguredHost(dir,data,false,true);
        return;
      }
      if(command=="--enable-autostart-json"||command=="--disable-autostart-json") {
        ConfigureAutostart(launcher,data,command=="--enable-autostart-json");FillAutostart(before,launcher);WriteJson(before);return;
      }
      bool stop=command=="--stop" || command=="--stop-json";
      if(!stop)ConfigureAutostart(launcher,data,null);
      if(stop) {
        StopAndWait(()=>ReadStatus(data,MonitorMutex).running,()=>{
          try{using(var signal=EventWaitHandle.OpenExisting(StopEvent))signal.Set();}catch(WaitHandleCannotBeOpenedException){}
        },5000);
        if(command=="--stop")return;
      } else if(!before.running) {
        StartAndWait(()=>ReadStatus(data,MonitorMutex).running,()=>QuotaPanelTaskHost.Run(launcher),5000);
      }
      if(jsonMode) {
        var after=ReadStatus(data,MonitorMutex);after.changed=before.running!=after.running;FillAutostart(after,launcher);WriteJson(after);
      }
    } catch(Exception error) {
      Environment.ExitCode=1;
      if(jsonMode)WriteJson(new {status="error",running=false,changed=false,visible=false,paused=false,message=QuotaOverlay.PanelLanguage.Text(QuotaOverlay.PanelLanguage.Current,"commandError")});
      else if(taskHostMode) {
        try {File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChatGPTQuotaOverlay","task-host-error.json"),new JavaScriptSerializer().Serialize(new {time=DateTime.UtcNow.ToString("o"),code=error.HResult}),Encoding.UTF8);}catch{}
      }
      else MessageBox.Show(error.Message,"Quota Panel",MessageBoxButtons.OK,MessageBoxIcon.Error);
    }
  }
  static void ConfigureAutostart(string launcher,string data,bool? enabled) {
    QuotaOverlay.Preferences.ConfigureAutostart(Path.Combine(data,"settings.json"),enabled,value=>{QuotaPanelTaskHost.ConfigureAutostart(launcher,value);});
  }
  static void FillAutostart(MonitorStatus status,string launcher) {
    var task=QuotaPanelTaskHost.Read(launcher);status.autoStartEnabled=task.autoStartEnabled;status.autoStartConfigured=task.autoStartConfigured;
  }
  static void WriteJson(object value) {
    using(var writer=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false))) {writer.WriteLine(new JavaScriptSerializer().Serialize(value));writer.Flush();}
  }
  static void StartConfiguredHost(string dir,string data,bool jsonMode,bool waitForHost) {
    var config=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(dir,"runtime.json")));
    string node=ResolveRuntimePath(dir,config["node"]),host=ResolveRuntimePath(dir,config["host"]);
    string codex=DiscoverCodex(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),config.ContainsKey("codex")?config["codex"]:"auto",dir);
    StartDetachedHost(node,host,Path.Combine(data,"state.json"),codex,jsonMode,waitForHost);
  }
  internal static void StartDetachedHost(string node,string host,string state,string codex,bool jsonMode,bool waitForHost=false) {
    // .NET Framework's Process.Start can inherit extra MCP pipe handles even when
    // all standard streams are redirected. Explicitly prohibit handle inheritance.
    // This environment change belongs only to this short-lived launcher process.
    Environment.SetEnvironmentVariable("QUOTA_CODEX_BIN",codex);
    var startup=new StartupInfo();startup.cb=Marshal.SizeOf(typeof(StartupInfo));
    ProcessInfo process;
    var command=new StringBuilder(Quote(node)+" "+Quote(host)+" --state "+Quote(state));
    if(!CreateProcess(node,command,IntPtr.Zero,IntPtr.Zero,false,0x08000000,IntPtr.Zero,Path.GetDirectoryName(host),ref startup,out process))throw new Exception(QuotaOverlay.PanelLanguage.Text(QuotaOverlay.PanelLanguage.Current,"launchError"));
    CloseHandle(process.thread);
    try {
      uint exitCode;
      if(WaitForSingleObject(process.process,waitForHost?UInt32.MaxValue:jsonMode?100u:1800u)==0 && GetExitCodeProcess(process.process,out exitCode) && exitCode!=0)throw new Exception(QuotaOverlay.PanelLanguage.Text(QuotaOverlay.PanelLanguage.Current,"startError"));
    } finally {CloseHandle(process.process);}
  }
  internal static string ResolveRuntimePath(string directory,string value) {
    if(Path.IsPathRooted(value))return Path.GetFullPath(value);
    return value.IndexOf('/')>=0 || value.IndexOf('\\')>=0 ? Path.GetFullPath(Path.Combine(directory,value)) : value;
  }
  internal static string DiscoverCodex(string localAppData,string configured,string directory) {
    var overrides=new[]{Environment.GetEnvironmentVariable("QUOTA_CODEX_BIN"),Environment.GetEnvironmentVariable("CODEX_CLI_PATH"),configured};
    foreach(string candidate in overrides)if(!String.IsNullOrWhiteSpace(candidate) && candidate!="auto") {
      string file=ResolveRuntimePath(directory,candidate);
      if(File.Exists(file) && Path.GetExtension(file).Equals(".exe",StringComparison.OrdinalIgnoreCase))return Path.GetFullPath(file);
    }
    string bins=Path.Combine(localAppData,"OpenAI","Codex","bin");
    if(Directory.Exists(bins)) {
      string latest=Directory.GetDirectories(bins).Select(folder=>Path.Combine(folder,"codex.exe")).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
      if(latest!=null)return latest;
    }
    foreach(string folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)) {
      try {string file=Path.Combine(folder.Trim('"'),"codex.exe");if(File.Exists(file))return file;}catch{}
    }
    throw new Exception(QuotaOverlay.PanelLanguage.Text(QuotaOverlay.PanelLanguage.Current,"codexMissing"));
  }
  internal static MonitorStatus ReadStatus(string data,string mutexName) {
    bool running;
    try{using(var mutex=Mutex.OpenExisting(mutexName))running=true;}catch(WaitHandleCannotBeOpenedException){running=false;}
    string language=QuotaOverlay.PanelLanguage.Read(Path.Combine(data,"settings.json"));
    var result=new MonitorStatus {running=running,status=running?"running":"stopped",message=QuotaOverlay.PanelLanguage.Text(language,running?"running":"stopped"),language=language};
    if(running)try {
      string file=Path.Combine(data,"state.json");
      if(DateTime.UtcNow-File.GetLastWriteTimeUtc(file)>TimeSpan.FromSeconds(10))return result;
      var state=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(file)) as Dictionary<string,object>;
      using(var process=Process.GetProcessById(Convert.ToInt32(state["process"])))if(process.ProcessName=="QuotaOverlay" && !process.HasExited) {
        result.visible=state.ContainsKey("visible") && Convert.ToBoolean(state["visible"]);
        result.paused=state.ContainsKey("paused") && Convert.ToBoolean(state["paused"]);
      }
    } catch {}
    return result;
  }
  internal static void StopAndWait(Func<bool> isRunning,Action requestStop,int timeoutMs) {
    var timer=Stopwatch.StartNew();
    do {
      if(!isRunning())return;
      // The singleton mutex exists before WPF finishes creating its stop event.
      requestStop();
      if(!isRunning())return;
      Thread.Sleep(50);
    }while(timer.ElapsedMilliseconds<timeoutMs);
    throw new Exception(QuotaOverlay.PanelLanguage.Text(QuotaOverlay.PanelLanguage.Current,"timeout"));
  }
  internal static void StartAndWait(Func<bool> isRunning,Action requestStart,int timeoutMs) {
    var timer=Stopwatch.StartNew();
    do {
      if(isRunning())return;
      // A stopped GUI can release its mutex before the old scheduled host exits.
      // Retry the demand request once that task becomes idle; IgnoreNew is safe.
      requestStart();
      if(isRunning())return;
      Thread.Sleep(100);
    }while(timer.ElapsedMilliseconds<timeoutMs);
    throw new Exception(QuotaOverlay.PanelLanguage.Text(QuotaOverlay.PanelLanguage.Current,"timeout"));
  }
  static string Quote(string value) { return "\""+value.Replace("\"","")+"\""; }
}
