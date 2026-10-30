using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Xml;

// A demand-only, least-privilege task gives the independently installed monitor
// an ordinary Windows service launch. It never runs at logon or on a timer.
internal static class QuotaPanelTaskHost {
  const string TaskNamespace="http://schemas.microsoft.com/windows/2004/02/mit/task";
  const string Marker="ChatGPTQuotaOverlay demand-only task host v1";
  const string AutoMarker="Quota Panel per-user logon host v2";
  internal class TaskStatus {public bool installed,enabled,running,autoStartConfigured,autoStartEnabled;}
  internal class TaskRecord {public string xml;public bool enabled,running;}
  internal interface ITaskStore : IDisposable {
    TaskRecord Read(string name);
    void Register(string name,string xml,string sid);
    void Update(string name,string xml,string sid);
    void Run(string name);
    void Remove(string name);
  }
  static string CurrentSid() {using(var identity=WindowsIdentity.GetCurrent())return identity.User.Value;}
  static string CheckedSid(string sid) {return new SecurityIdentifier(sid).Value;}
  internal static string TaskName(string sid) {return "ChatGPTQuotaOverlay.Host."+CheckedSid(sid);}
  static string LauncherPath(string launcherPath) {
    if(String.IsNullOrWhiteSpace(launcherPath) || !Path.IsPathRooted(launcherPath) || launcherPath.StartsWith(@"\\",StringComparison.Ordinal) || !Path.GetExtension(launcherPath).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Нужен полный локальный путь к программе панели.","launcherPath");
    return Path.GetFullPath(launcherPath);
  }
  internal static string CreateTaskXml(string launcherPath,string userSid) {
    string launcher=LauncherPath(launcherPath),sid=CheckedSid(userSid);
    var output=new StringBuilder();
    using(var xml=XmlWriter.Create(output,new XmlWriterSettings {OmitXmlDeclaration=true,Indent=false})) {
      xml.WriteStartElement("Task",TaskNamespace);xml.WriteAttributeString("version","1.2");
      xml.WriteStartElement("RegistrationInfo");
      xml.WriteElementString("Author",sid);xml.WriteElementString("Description",Marker);xml.WriteElementString("URI",@"\"+TaskName(sid));xml.WriteEndElement();
      xml.WriteStartElement("Triggers");xml.WriteEndElement();
      xml.WriteStartElement("Principals");xml.WriteStartElement("Principal");xml.WriteAttributeString("id","CurrentUser");
      xml.WriteElementString("UserId",sid);xml.WriteElementString("LogonType","InteractiveToken");xml.WriteElementString("RunLevel","LeastPrivilege");xml.WriteEndElement();xml.WriteEndElement();
      xml.WriteStartElement("Settings");
      xml.WriteElementString("MultipleInstancesPolicy","IgnoreNew");
      xml.WriteElementString("DisallowStartIfOnBatteries","false");xml.WriteElementString("StopIfGoingOnBatteries","false");
      xml.WriteElementString("AllowHardTerminate","true");xml.WriteElementString("StartWhenAvailable","false");
      xml.WriteElementString("RunOnlyIfNetworkAvailable","false");xml.WriteElementString("AllowStartOnDemand","true");
      xml.WriteElementString("Enabled","true");xml.WriteElementString("Hidden","false");xml.WriteElementString("RunOnlyIfIdle","false");xml.WriteElementString("WakeToRun","false");
      xml.WriteElementString("ExecutionTimeLimit","PT0S");xml.WriteElementString("Priority","7");xml.WriteEndElement();
      xml.WriteStartElement("Actions");xml.WriteAttributeString("Context","CurrentUser");xml.WriteStartElement("Exec");
      xml.WriteElementString("Command",launcher);xml.WriteElementString("Arguments","--run-host");xml.WriteElementString("WorkingDirectory",Path.GetDirectoryName(launcher));
      xml.WriteEndElement();xml.WriteEndElement();xml.WriteEndElement();
    }
    return output.ToString();
  }
  static string Text(XmlDocument doc,XmlNamespaceManager ns,string path) {var nodes=doc.SelectNodes(path,ns);return nodes.Count==1?nodes[0].InnerText:null;}
  static bool EqualsText(XmlDocument doc,XmlNamespaceManager ns,string path,string expected) {return Text(doc,ns,path)==expected;}
  // Task Scheduler omits values equal to its documented schema defaults when
  // exporting XML. Missing values are accepted only for those explicit defaults.
  static bool EqualsDefault(XmlDocument doc,XmlNamespaceManager ns,string path,string expected) {return doc.SelectNodes(path,ns).Count==0 || EqualsText(doc,ns,path,expected);}
  static bool SamePath(string actual,string expected) {return actual!=null && Path.IsPathRooted(actual) && String.Equals(Path.GetFullPath(actual),expected,StringComparison.OrdinalIgnoreCase);}
  static XmlDocument Parse(string xml) {
    if(String.IsNullOrEmpty(xml)||xml.Length>65536)throw new ArgumentException("Invalid task XML");
    var doc=new XmlDocument();doc.XmlResolver=null;
    using(var input=XmlReader.Create(new StringReader(xml),new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))doc.Load(input);
    return doc;
  }
  static XmlNamespaceManager Namespaces(XmlDocument doc) {var ns=new XmlNamespaceManager(doc.NameTable);ns.AddNamespace("t",TaskNamespace);return ns;}
  static bool SameIdentity(string actual,string sid) {
    if(String.IsNullOrWhiteSpace(actual))return false;
    try{return new SecurityIdentifier(actual).Value==sid;}
    catch(ArgumentException){try{return ((SecurityIdentifier)new NTAccount(actual).Translate(typeof(SecurityIdentifier))).Value==sid;}catch{return false;}}
  }
  internal static bool IsOwnTaskXml(string xml,string launcherPath,string userSid) {
    try {
      var doc=Parse(xml);var ns=Namespaces(doc);
      if(Text(doc,ns,"/t:Task/t:RegistrationInfo/t:Description")!=AutoMarker)return IsLegacyTaskXml(xml,launcherPath,userSid);
      string sid=CheckedSid(userSid);
      if(doc.SelectNodes("/t:Task/t:Triggers/*",ns).Count!=1||doc.SelectNodes("/t:Task/t:Triggers/t:LogonTrigger",ns).Count!=1)return false;
      if(!SameIdentity(Text(doc,ns,"/t:Task/t:Triggers/t:LogonTrigger/t:UserId"),sid))return false;
      string enabled=Text(doc,ns,"/t:Task/t:Triggers/t:LogonTrigger/t:Enabled");
      if(enabled!=null&&enabled!="true"&&enabled!="false")return false;
      if(doc.SelectNodes("/t:Task/t:Triggers/t:LogonTrigger/*[not(self::t:UserId or self::t:Enabled)]",ns).Count!=0)return false;
      if(doc.SelectNodes("/t:Task/t:Triggers/t:LogonTrigger/t:UserId",ns).Count!=1||doc.SelectNodes("/t:Task/t:Triggers/t:LogonTrigger/t:Enabled",ns).Count>1)return false;
      if(doc.SelectNodes("/t:Task/t:Settings/t:RestartOnFailure",ns).Count!=1||!EqualsText(doc,ns,"/t:Task/t:Settings/t:RestartOnFailure/t:Interval","PT1M")||!EqualsText(doc,ns,"/t:Task/t:Settings/t:RestartOnFailure/t:Count","3"))return false;
      if(doc.SelectNodes("/t:Task/t:Settings/t:RestartOnFailure/*",ns).Count!=2)return false;
      doc.SelectSingleNode("/t:Task/t:RegistrationInfo/t:Description",ns).InnerText=Marker;
      doc.SelectSingleNode("/t:Task/t:Triggers",ns).RemoveAll();var restart=doc.SelectSingleNode("/t:Task/t:Settings/t:RestartOnFailure",ns);restart.ParentNode.RemoveChild(restart);
      return IsLegacyTaskXml(doc.OuterXml,launcherPath,userSid);
    }catch{return false;}
  }
  static bool IsLegacyTaskXml(string xml,string launcherPath,string userSid) {
    try {
      string launcher=LauncherPath(launcherPath),sid=CheckedSid(userSid);
      if(String.IsNullOrEmpty(xml) || xml.Length>65536)return false;
      var doc=new XmlDocument();doc.XmlResolver=null;
      using(var input=XmlReader.Create(new StringReader(xml),new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))doc.Load(input);
      var ns=new XmlNamespaceManager(doc.NameTable);ns.AddNamespace("t",TaskNamespace);
      if(doc.DocumentElement.LocalName!="Task" || doc.DocumentElement.NamespaceURI!=TaskNamespace)return false;
      if(!EqualsText(doc,ns,"/t:Task/t:RegistrationInfo/t:Author",sid) || !EqualsText(doc,ns,"/t:Task/t:RegistrationInfo/t:Description",Marker) || !EqualsText(doc,ns,"/t:Task/t:RegistrationInfo/t:URI",@"\"+TaskName(sid)))return false;
      if(doc.SelectNodes("/t:Task/t:Triggers",ns).Count!=1 || doc.SelectNodes("/t:Task/t:Triggers/*",ns).Count!=0)return false;
      if(doc.SelectNodes("/t:Task/t:Principals/t:Principal",ns).Count!=1 || doc.SelectNodes("/t:Task/t:Actions/*",ns).Count!=1 || doc.SelectNodes("/t:Task/t:Actions/t:Exec",ns).Count!=1)return false;
      if(!EqualsText(doc,ns,"/t:Task/t:Principals/t:Principal/@id","CurrentUser") || !EqualsText(doc,ns,"/t:Task/t:Actions/@Context","CurrentUser"))return false;
      if(!EqualsText(doc,ns,"/t:Task/t:Principals/t:Principal/t:UserId",sid) || !EqualsText(doc,ns,"/t:Task/t:Principals/t:Principal/t:LogonType","InteractiveToken") || !EqualsDefault(doc,ns,"/t:Task/t:Principals/t:Principal/t:RunLevel","LeastPrivilege"))return false;
      if(doc.SelectNodes("/t:Task/t:Principals/t:Principal/t:GroupId",ns).Count!=0)return false;
      if(!SamePath(Text(doc,ns,"/t:Task/t:Actions/t:Exec/t:Command"),launcher) || !EqualsText(doc,ns,"/t:Task/t:Actions/t:Exec/t:Arguments","--run-host") || !SamePath(Text(doc,ns,"/t:Task/t:Actions/t:Exec/t:WorkingDirectory"),Path.GetDirectoryName(launcher)))return false;
      if(!EqualsText(doc,ns,"/t:Task/t:Settings/t:MultipleInstancesPolicy","IgnoreNew") || !EqualsText(doc,ns,"/t:Task/t:Settings/t:ExecutionTimeLimit","PT0S") || !EqualsDefault(doc,ns,"/t:Task/t:Settings/t:AllowStartOnDemand","true"))return false;
      foreach(string name in new[]{"DisallowStartIfOnBatteries","StopIfGoingOnBatteries"})if(!EqualsText(doc,ns,"/t:Task/t:Settings/t:"+name,"false"))return false;
      foreach(string name in new[]{"Hidden","StartWhenAvailable","RunOnlyIfNetworkAvailable","RunOnlyIfIdle","WakeToRun"})if(!EqualsDefault(doc,ns,"/t:Task/t:Settings/t:"+name,"false"))return false;
      string enabled=Text(doc,ns,"/t:Task/t:Settings/t:Enabled");if(doc.SelectNodes("/t:Task/t:Settings/t:Enabled",ns).Count!=0 && enabled!="true" && enabled!="false")return false;
      if(!EqualsDefault(doc,ns,"/t:Task/t:Settings/t:AllowHardTerminate","true") || !EqualsDefault(doc,ns,"/t:Task/t:Settings/t:Priority","7"))return false;
      if(doc.SelectNodes("/t:Task/t:Settings/t:RestartOnFailure | /t:Task/t:Settings/t:MaintenanceSettings",ns).Count!=0)return false;
      return true;
    }catch{return false;}
  }
  static string PreviousLauncher(TaskRecord record,string launcher,string sid) {
    try {
      string suffix=Path.Combine("dist","overlay","ChatGPTLimitPanelLauncher.exe");
      if(!LauncherPath(launcher).EndsWith(Path.DirectorySeparatorChar+suffix,StringComparison.OrdinalIgnoreCase))return null;
      var doc=Parse(record.xml);var ns=Namespaces(doc);
      string previous=LauncherPath(Text(doc,ns,"/t:Task/t:Actions/t:Exec/t:Command"));
      // Older published packages used QuotaMonitor.exe in the same relative directory.
      bool known=previous.EndsWith(Path.DirectorySeparatorChar+suffix,StringComparison.OrdinalIgnoreCase)||previous.EndsWith(Path.DirectorySeparatorChar+Path.Combine("dist","overlay","QuotaMonitor.exe"),StringComparison.OrdinalIgnoreCase);
      return known&&IsOwnTaskXml(record.xml,previous,sid)?previous:null;
    }catch{return null;}
  }
  static TaskRecord Migrate(TaskRecord record,string launcher,string sid,ITaskStore store) {
    if(record==null||IsOwnTaskXml(record.xml,launcher,sid))return record;
    if(PreviousLauncher(record,launcher,sid)==null){Validate(record,launcher,sid);return record;}
    if(record.running)throw new InvalidOperationException("Сначала останови предыдущую панель, затем запусти новую версию.");
    string expected=record.xml;bool enabled=record.enabled;
    var doc=Parse(expected);var ns=Namespaces(doc);
    doc.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:Command",ns).InnerText=LauncherPath(launcher);
    doc.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:WorkingDirectory",ns).InnerText=Path.GetDirectoryName(LauncherPath(launcher));
    string name=TaskName(sid);var latest=store.Read(name);
    // Recheck immediately before updating; never overwrite a replaced or running task.
    if(latest==null||latest.running||latest.enabled!=enabled||latest.xml!=expected)throw new InvalidOperationException("Задача запуска изменилась. Повтори обновление после остановки панели.");
    store.Update(name,doc.OuterXml,sid);
    var migrated=store.Read(name);Validate(migrated,launcher,sid);
    if(migrated==null||migrated.enabled!=enabled)throw new InvalidOperationException("Не удалось подтвердить перенос задачи запуска панели.");
    return migrated;
  }
  static void Validate(TaskRecord record,string launcher,string sid) {
    if(record!=null && !IsOwnTaskXml(record.xml,launcher,sid))throw new InvalidOperationException("Задача запуска с этим именем не принадлежит этой установке панели. Изменения не выполнены.");
  }
  static TaskStatus Status(TaskRecord record) {
    bool configured=false,auto=false;
    if(record!=null){var doc=Parse(record.xml);var ns=Namespaces(doc);configured=Text(doc,ns,"/t:Task/t:RegistrationInfo/t:Description")==AutoMarker;auto=configured&&Text(doc,ns,"/t:Task/t:Triggers/t:LogonTrigger/t:Enabled")!="false";}
    return new TaskStatus {installed=record!=null,enabled=record!=null&&record.enabled,running=record!=null&&record.running,autoStartConfigured=configured,autoStartEnabled=auto};
  }
  internal static TaskStatus ConfigureAutostart(string launcherPath,bool enabled) {using(var store=new SchedulerStore())return ConfigureAutostart(launcherPath,CurrentSid(),store,enabled);}
  internal static TaskStatus ConfigureAutostart(string launcherPath,string sid,ITaskStore store,bool enabled) {
    string name=TaskName(sid);var record=Migrate(store.Read(name),launcherPath,sid,store);Validate(record,launcherPath,sid);
    if(record!=null&&Status(record).autoStartConfigured&&Status(record).autoStartEnabled==enabled)return Status(record);
    var doc=Parse(CreateTaskXml(launcherPath,sid));var ns=Namespaces(doc);
    doc.SelectSingleNode("/t:Task/t:RegistrationInfo/t:Description",ns).InnerText=AutoMarker;
    var trigger=doc.CreateElement("LogonTrigger",TaskNamespace);
    var flag=doc.CreateElement("Enabled",TaskNamespace);flag.InnerText=enabled?"true":"false";trigger.AppendChild(flag);
    var user=doc.CreateElement("UserId",TaskNamespace);user.InnerText=CheckedSid(sid);trigger.AppendChild(user);doc.SelectSingleNode("/t:Task/t:Triggers",ns).AppendChild(trigger);
    var restart=doc.CreateElement("RestartOnFailure",TaskNamespace);var interval=doc.CreateElement("Interval",TaskNamespace);interval.InnerText="PT1M";restart.AppendChild(interval);var count=doc.CreateElement("Count",TaskNamespace);count.InnerText="3";restart.AppendChild(count);doc.SelectSingleNode("/t:Task/t:Settings",ns).AppendChild(restart);
    if(record!=null&&!record.enabled)doc.SelectSingleNode("/t:Task/t:Settings/t:Enabled",ns).InnerText="false";
    if(record==null) {
      try{store.Register(name,doc.OuterXml,sid);}
      catch{record=store.Read(name);Validate(record,launcherPath,sid);if(record==null||!Status(record).autoStartConfigured||Status(record).autoStartEnabled!=enabled)throw;return Status(record);}
    }else store.Update(name,doc.OuterXml,sid);
    record=store.Read(name);Validate(record,launcherPath,sid);if(record==null)throw new InvalidOperationException("Задача автозапуска не была создана.");return Status(record);
  }
  internal static TaskStatus Read(string launcherPath) {using(var store=new SchedulerStore())return Read(launcherPath,CurrentSid(),store);}
  internal static TaskStatus Ensure(string launcherPath) {using(var store=new SchedulerStore())return Ensure(launcherPath,CurrentSid(),store);}
  internal static TaskStatus Run(string launcherPath) {using(var store=new SchedulerStore())return Run(launcherPath,CurrentSid(),store);}
  internal static TaskStatus RemoveOwn(string launcherPath) {using(var store=new SchedulerStore())return RemoveOwn(launcherPath,CurrentSid(),store);}
  internal static TaskStatus Read(string launcherPath,string sid,ITaskStore store) {
    var record=store.Read(TaskName(sid));
    if(record!=null&&!IsOwnTaskXml(record.xml,launcherPath,sid)&&PreviousLauncher(record,launcherPath,sid)==null)Validate(record,launcherPath,sid);
    return Status(record);
  }
  internal static TaskStatus Ensure(string launcherPath,string sid,ITaskStore store) {
    string name=TaskName(sid);var record=Migrate(store.Read(name),launcherPath,sid,store);Validate(record,launcherPath,sid);
    if(record==null) {
      // Create-only avoids overwriting a task created between the read and write.
      try {store.Register(name,CreateTaskXml(launcherPath,sid),sid);}
      catch {record=store.Read(name);if(record==null)throw;Validate(record,launcherPath,sid);return Status(record);}
      record=store.Read(name);Validate(record,launcherPath,sid);
      if(record==null)throw new InvalidOperationException("Задача запуска панели не была создана.");
    }
    return Status(record);
  }
  internal static TaskStatus Run(string launcherPath,string sid,ITaskStore store) {
    string name=TaskName(sid);var record=store.Read(name);Validate(record,launcherPath,sid);
    if(record==null)throw new InvalidOperationException("Задача запуска панели ещё не создана.");
    if(!record.enabled)throw new InvalidOperationException("Задача запуска панели отключена в Планировщике Windows. Включи её для запуска панели.");
    if(!record.running)store.Run(name);
    return Status(store.Read(name));
  }
  internal static TaskStatus RemoveOwn(string launcherPath,string sid,ITaskStore store) {
    string name=TaskName(sid);var record=store.Read(name);Validate(record,launcherPath,sid);
    if(record!=null) {if(record.running)throw new InvalidOperationException("Сначала останови панель, затем удали её задачу запуска.");store.Remove(name);}
    return new TaskStatus();
  }
  // Reflection keeps the .NET Framework build free of dynamic/interop packages.
  sealed class SchedulerStore : ITaskStore {
    object service,folder;
    static object Call(object target,string name,params object[] args) {return target.GetType().InvokeMember(name,BindingFlags.InvokeMethod|BindingFlags.OptionalParamBinding,null,target,args,CultureInfo.InvariantCulture);}
    static object Get(object target,string name) {return target.GetType().InvokeMember(name,BindingFlags.GetProperty,null,target,null,CultureInfo.InvariantCulture);}
    static void Release(object value) {if(value!=null && Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
    static int ErrorCode(Exception error) {while(error is TargetInvocationException && error.InnerException!=null)error=error.InnerException;return Marshal.GetHRForException(error);}
    internal SchedulerStore() {
      try {service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service",true));Call(service,"Connect",Type.Missing,Type.Missing,Type.Missing,Type.Missing);folder=Call(service,"GetFolder",@"\");}
      catch {Dispose();throw;}
    }
    public TaskRecord Read(string name) {
      object task=null;
      try {task=Call(folder,"GetTask",name);return new TaskRecord {xml=Convert.ToString(Get(task,"Xml"),CultureInfo.InvariantCulture),enabled=Convert.ToBoolean(Get(task,"Enabled"),CultureInfo.InvariantCulture),running=Convert.ToInt32(Get(task,"State"),CultureInfo.InvariantCulture)==4};}
      catch(Exception error) {int code=ErrorCode(error);if(code==unchecked((int)0x80070002) || code==unchecked((int)0x80070003))return null;throw;}
      finally {Release(task);}
    }
    public void Register(string name,string xml,string sid) {object task=null;try{task=Call(folder,"RegisterTask",name,xml,2,sid,null,3,null);}finally{Release(task);}}
    public void Update(string name,string xml,string sid) {object task=null;try{task=Call(folder,"RegisterTask",name,xml,4,sid,null,3,null);}finally{Release(task);}}
    public void Run(string name) {object task=null,instance=null;try{task=Call(folder,"GetTask",name);instance=Call(task,"Run",new object[]{null});}finally{Release(instance);Release(task);}}
    public void Remove(string name) {Call(folder,"DeleteTask",name,0);}
    public void Dispose() {Release(folder);folder=null;Release(service);service=null;}
  }
}
