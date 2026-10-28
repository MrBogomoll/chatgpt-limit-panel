using System;
using System.IO;
using System.Xml;

class OverlayTaskHostTest {
  const string Sid="S-1-5-21-100-200-300-1001";
  const string Launcher=@"C:\Users\Тест & User\AppData\Local\ChatGPTQuotaOverlay\app\dist\overlay\ChatGPTLimitPanelLauncher.exe";
  // Literal shape exported by Windows Task Scheduler after registration. The
  // service elides documented defaults and assigns the registered task URI.
  const string WindowsXml=@"<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
    <RegistrationInfo><Author>S-1-5-21-100-200-300-1001</Author><Description>ChatGPTQuotaOverlay demand-only task host v1</Description><URI>\ChatGPTQuotaOverlay.Host.S-1-5-21-100-200-300-1001</URI></RegistrationInfo>
    <Principals><Principal id=""CurrentUser""><UserId>S-1-5-21-100-200-300-1001</UserId><LogonType>InteractiveToken</LogonType></Principal></Principals>
    <Settings><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><IdleSettings><StopOnIdleEnd>true</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings></Settings>
    <Triggers />
    <Actions Context=""CurrentUser""><Exec><Command>C:\Users\Тест &amp; User\AppData\Local\ChatGPTQuotaOverlay\app\dist\overlay\ChatGPTLimitPanelLauncher.exe</Command><Arguments>--run-host</Arguments><WorkingDirectory>C:\Users\Тест &amp; User\AppData\Local\ChatGPTQuotaOverlay\app\dist\overlay</WorkingDirectory></Exec></Actions>
  </Task>";
  static void Check(bool value,string message) {if(!value)throw new Exception(message);}
  static XmlDocument Parse(string xml) {var doc=new XmlDocument();doc.LoadXml(xml);return doc;}
  static string Value(XmlDocument doc,string path) {var ns=new XmlNamespaceManager(doc.NameTable);ns.AddNamespace("t","http://schemas.microsoft.com/windows/2004/02/mit/task");var node=doc.SelectSingleNode(path,ns);return node==null?null:node.InnerText;}
  static void Rejected(string xml,string message) {Check(!QuotaPanelTaskHost.IsOwnTaskXml(xml,Launcher,Sid),message);}
  static void Throws(Action action,string contains) {try{action();}catch(InvalidOperationException error){Check(error.Message.Contains(contains),"clear error: "+error.Message);return;}throw new Exception("expected rejection: "+contains);}
  class Store : QuotaPanelTaskHost.ITaskStore {
    internal QuotaPanelTaskHost.TaskRecord record;
    internal int registrations,runs,removals;
    internal string name;
    public QuotaPanelTaskHost.TaskRecord Read(string taskName) {name=taskName;return record;}
    public void Register(string taskName,string xml,string sid) {Check(record==null,"existing tasks must never be overwritten");registrations++;name=taskName;record=new QuotaPanelTaskHost.TaskRecord {xml=xml,enabled=true,running=false};}
    public void Update(string taskName,string xml,string sid) {throw new Exception("legacy demand-only operations must not update existing tasks");}
    public void Run(string taskName) {Check(record!=null && record.enabled,"task must already be enabled");runs++;record.running=true;}
    public void Remove(string taskName) {removals++;record=null;}
    public void Dispose() {}
  }
  public static int Main() {
    try {
      string xml=QuotaPanelTaskHost.CreateTaskXml(Launcher,Sid);
      var doc=Parse(xml);
      Check(Value(doc,"/t:Task/t:Triggers")=="","no event, app, logon, or startup triggers");
      Check(Value(doc,"/t:Task/t:Principals/t:Principal/t:UserId")==Sid,"only current user's SID");
      Check(Value(doc,"/t:Task/t:Principals/t:Principal/t:LogonType")=="InteractiveToken","interactive session only, without password");
      Check(Value(doc,"/t:Task/t:Principals/t:Principal/t:RunLevel")=="LeastPrivilege","no privilege elevation");
      Check(Value(doc,"/t:Task/t:Actions/t:Exec/t:Command")==Launcher,"XML preserves Cyrillic, ampersand, and spaces in exact executable path");
      Check(Value(doc,"/t:Task/t:Actions/t:Exec/t:Arguments")=="--run-host","scheduler owns the persistent host mode");
      Check(Value(doc,"/t:Task/t:Settings/t:ExecutionTimeLimit")=="PT0S","monitor is not terminated after default 72 hours");
      Check(Value(doc,"/t:Task/t:Settings/t:MultipleInstancesPolicy")=="IgnoreNew","duplicate launch cannot kill or multiply monitor");
      Check(Value(doc,"/t:Task/t:Settings/t:AllowStartOnDemand")=="true","task starts only on explicit demand");
      foreach(string setting in new[]{"Hidden","DisallowStartIfOnBatteries","StopIfGoingOnBatteries","RunOnlyIfIdle","WakeToRun"})Check(Value(doc,"/t:Task/t:Settings/t:"+setting)=="false",setting+" is disabled");
      Check(Value(doc,"/t:Task/t:Settings/t:RestartOnFailure")==null,"manual stop has no restart policy");
      Check(QuotaPanelTaskHost.IsOwnTaskXml(xml,Launcher,Sid),"generated task is recognized as own task");
      Check(QuotaPanelTaskHost.IsOwnTaskXml(WindowsXml,Launcher,Sid),"Windows-normalized task retains ownership when documented defaults are omitted");
      Check(QuotaPanelTaskHost.IsOwnTaskXml(WindowsXml.Replace("<Settings>","<Settings><Enabled>false</Enabled>"),Launcher,Sid),"normalized manually disabled task retains ownership");
      Rejected(WindowsXml.Replace("</Principal>","<RunLevel>HighestAvailable</RunLevel></Principal>"),"normalized definition cannot elevate privilege");
      foreach(string entry in new[]{"<Hidden>true</Hidden>","<StartWhenAvailable>true</StartWhenAvailable>","<RunOnlyIfNetworkAvailable>true</RunOnlyIfNetworkAvailable>","<AllowStartOnDemand>false</AllowStartOnDemand>","<RunOnlyIfIdle>true</RunOnlyIfIdle>","<WakeToRun>true</WakeToRun>","<AllowHardTerminate>false</AllowHardTerminate>","<Priority>3</Priority>"})Rejected(WindowsXml.Replace("<Settings>","<Settings>"+entry),"explicit non-default setting is rejected: "+entry);
      Rejected(WindowsXml.Replace("<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>",""),"battery permission has no safe omitted default");
      Rejected(WindowsXml.Replace("<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>",""),"infinite task lifetime must be explicit");
      Rejected(WindowsXml.Replace("\\ChatGPTQuotaOverlay.Host.","\\UnrelatedTask."),"normalized URI must name this user's exact task");
      Check(QuotaPanelTaskHost.IsOwnTaskXml(xml.Replace("<Enabled>true</Enabled>","<Enabled>false</Enabled>"),Launcher,Sid),"Windows can persist a disabled task without losing ownership");
      string currentLauncher=Path.Combine(Environment.CurrentDirectory,"ChatGPTLimitPanelLauncher.exe");
      string relativeCommand=QuotaPanelTaskHost.CreateTaskXml(currentLauncher,Sid).Replace(currentLauncher,"ChatGPTLimitPanelLauncher.exe");
      Check(!QuotaPanelTaskHost.IsOwnTaskXml(relativeCommand,currentLauncher,Sid),"task action must be absolute even when a relative path currently resolves to same file");
      Rejected(xml.Replace("<Triggers />","<Triggers><LogonTrigger><Enabled>true</Enabled></LogonTrigger></Triggers>"),"reject unrequested startup triggers");
      Rejected(xml.Replace("--run-host","--start"),"reject an altered executable action");
      Rejected(xml.Replace("LeastPrivilege","HighestAvailable"),"reject privilege elevation");
      Rejected(xml.Replace("PT0S","PT72H"),"reject task lifetime limit");
      Rejected(xml.Replace("IgnoreNew","StopExisting"),"reject replacement instance policy");
      Rejected(xml.Replace("<Hidden>false</Hidden>","<Hidden>true</Hidden>"),"reject hidden task definitions");
      Rejected(xml.Replace(Sid,"S-1-5-21-100-200-300-1002"),"reject another user's task");
      Rejected(xml.Replace("ChatGPTLimitPanelLauncher.exe","Unrelated.exe"),"reject another executable");
      Rejected(xml.Replace("<Actions Context=\"CurrentUser\">","<Actions Context=\"CurrentUser\"><Exec><Command>calc.exe</Command></Exec>"),"reject extra action");
      Rejected("<!DOCTYPE Task [<!ENTITY x SYSTEM 'file:///C:/Windows/win.ini'>]><Task>&x;</Task>","reject external entities");
      Rejected("not xml","reject malformed definitions");
      var store=new Store();
      var missing=QuotaPanelTaskHost.Read(Launcher,Sid,store);
      Check(!missing.installed && !missing.enabled && !missing.running,"missing task reports uninstalled without registration");
      var ready=QuotaPanelTaskHost.Ensure(Launcher,Sid,store);
      Check(ready.installed && ready.enabled && !ready.running && store.registrations==1,"ensure registers a demand-only task without running it");
      Check(store.name=="ChatGPTQuotaOverlay.Host.S-1-5-21-100-200-300-1001","task name belongs to current SID");
      QuotaPanelTaskHost.Ensure(Launcher,Sid,store);
      Check(store.registrations==1 && store.runs==0,"repeat ensure cannot modify or launch existing task");
      QuotaPanelTaskHost.Run(Launcher,Sid,store);
      Check(QuotaPanelTaskHost.Read(Launcher,Sid,store).running && store.runs==1,"explicit run starts the validated own task");
      QuotaPanelTaskHost.Run(Launcher,Sid,store);
      Check(store.runs==1,"already running host remains untouched");
      store.record.running=false;store.record.enabled=false;
      Check(!QuotaPanelTaskHost.Ensure(Launcher,Sid,store).enabled && store.registrations==1,"ensure preserves manually disabled task");
      Throws(()=>QuotaPanelTaskHost.Run(Launcher,Sid,store),"отключена");
      Check(store.runs==1,"disabled task never runs");
      store.record.xml=xml.Replace("ChatGPTLimitPanelLauncher.exe","Unrelated.exe");
      Throws(()=>QuotaPanelTaskHost.Ensure(Launcher,Sid,store),"не принадлежит");
      Throws(()=>QuotaPanelTaskHost.Run(Launcher,Sid,store),"не принадлежит");
      Throws(()=>QuotaPanelTaskHost.RemoveOwn(Launcher,Sid,store),"не принадлежит");
      Check(store.registrations==1 && store.runs==1 && store.removals==0,"foreign task is never rewritten, run, or removed");
      store.record.xml=xml;store.record.running=true;
      Throws(()=>QuotaPanelTaskHost.RemoveOwn(Launcher,Sid,store),"останови");
      store.record.running=false;
      QuotaPanelTaskHost.RemoveOwn(Launcher,Sid,store);
      Check(!QuotaPanelTaskHost.Read(Launcher,Sid,store).installed && store.removals==1,"uninstall removes only a validated stopped task");
      Console.WriteLine("PASS demand-only task ownership, XML, manual disable, explicit start and removal");return 0;
    }catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
  }
}
