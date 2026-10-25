using System;
using System.Reflection;
using System.Xml;
class OverlayAutostartTest {
  const string Sid="S-1-5-21-100-200-300-1001",Launcher=@"C:\Users\Другой User\.codex\plugins\cache\market\quota-panel\0.2.0-beta.3\dist\overlay\QuotaMonitor.exe";
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  static MethodInfo Method(string name,int count){foreach(var m in typeof(QuotaPanelTaskHost).GetMethods(BindingFlags.Static|BindingFlags.NonPublic))if(m.Name==name&&m.GetParameters().Length==count)return m;throw new Exception("missing portable autostart operation "+name);}
  static object Call(string name,params object[] args){try{return Method(name,args.Length).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
  static string Value(string xml,string query){var doc=new XmlDocument();doc.LoadXml(xml);var ns=new XmlNamespaceManager(doc.NameTable);ns.AddNamespace("t",doc.DocumentElement.NamespaceURI);var node=doc.SelectSingleNode(query,ns);return node==null?null:node.InnerText;}
  class Store:QuotaPanelTaskHost.ITaskStore {
    public QuotaPanelTaskHost.TaskRecord record;public int creates,updates,runs;
    public QuotaPanelTaskHost.TaskRecord Read(string name){return record;}
    public void Register(string name,string xml,string sid){Check(record==null,"create must not overwrite");creates++;record=new QuotaPanelTaskHost.TaskRecord {xml=xml,enabled=true};}
    public void Update(string name,string xml,string sid){Check(record!=null,"update requires an existing own record");updates++;record.xml=xml;}
    public void Run(string name){runs++;record.running=true;}
    public void Remove(string name){record=null;}
    public void Dispose(){}
  }
  public static int Main(string[] args){try{
    if(args.Length==2){using(var identity=System.Security.Principal.WindowsIdentity.GetCurrent())Check(QuotaPanelTaskHost.IsOwnTaskXml(System.IO.File.ReadAllText(args[0]),args[1],identity.User.Value),"actual Windows-exported task must validate against current installed path and user");Console.WriteLine("PASS actual Windows-exported autostart task ownership");return 0;}
    var store=new Store();Call("ConfigureAutostart",Launcher,Sid,store,true);
    Check(store.creates==1,"first activation must create a per-user task");string xml=store.record.xml;
    Check(Value(xml,"/t:Task/t:Triggers/t:LogonTrigger/t:UserId")==Sid,"logon trigger must use the installing user's SID");
    Check(Value(xml,"/t:Task/t:Triggers/t:LogonTrigger/t:Enabled")=="true","autostart must default on");
    Check(Value(xml,"/t:Task/t:Principals/t:Principal/t:RunLevel")=="LeastPrivilege","autostart must not request elevation");
    Check(Value(xml,"/t:Task/t:Actions/t:Exec/t:Command")==Launcher,"use this user's installed path, including spaces/Cyrillic");
    Check(QuotaPanelTaskHost.IsOwnTaskXml(xml,Launcher,Sid),"v2 registered XML must retain ownership");
    using(var identity=System.Security.Principal.WindowsIdentity.GetCurrent()) {
      string actualSid=identity.User.Value;var exported=new Store();Call("ConfigureAutostart",Launcher,actualSid,exported,true);
      var normalized=new XmlDocument();normalized.LoadXml(exported.record.xml);var ns=new XmlNamespaceManager(normalized.NameTable);ns.AddNamespace("t",normalized.DocumentElement.NamespaceURI);
      normalized.SelectSingleNode("/t:Task/t:Triggers/t:LogonTrigger/t:UserId",ns).InnerText=identity.Name;
      var flag=normalized.SelectSingleNode("/t:Task/t:Triggers/t:LogonTrigger/t:Enabled",ns);flag.ParentNode.RemoveChild(flag);
      Check(QuotaPanelTaskHost.IsOwnTaskXml(normalized.OuterXml,Launcher,actualSid),"Windows export may normalize a logon SID into its account name and omit default Enabled");
    }
    Call("ConfigureAutostart",Launcher,Sid,store,true);Check(store.creates==1&&store.updates==0,"repeat setup must be idempotent");
    Call("ConfigureAutostart",Launcher,Sid,store,false);Check(Value(store.record.xml,"/t:Task/t:Triggers/t:LogonTrigger/t:Enabled")=="false","disabling autostart must only disable its trigger");
    QuotaPanelTaskHost.Run(Launcher,Sid,store);Check(store.runs==1,"manual start remains available with logon disabled");
    store.record=new QuotaPanelTaskHost.TaskRecord {xml=QuotaPanelTaskHost.CreateTaskXml(Launcher,Sid),enabled=false};
    Call("ConfigureAutostart",Launcher,Sid,store,true);Check(!store.record.enabled&&Value(store.record.xml,"/t:Task/t:Settings/t:Enabled")=="false","migration must preserve a manually disabled task");
    Check(QuotaPanelTaskHost.IsOwnTaskXml(store.record.xml,Launcher,Sid),"disabled migrated task stays owned");
    Check(!QuotaPanelTaskHost.IsOwnTaskXml(xml.Replace("<UserId>"+Sid+"</UserId>","<UserId>S-1-5-21-9-9-9-1001</UserId>"),Launcher,Sid),"reject another user's trigger/principal");
    Check(!QuotaPanelTaskHost.IsOwnTaskXml(xml.Replace("LeastPrivilege","HighestAvailable"),Launcher,Sid),"reject elevated definitions");
    store.record.xml=xml.Replace("QuotaMonitor.exe","other.exe");int changes=store.updates;
    bool rejected=false;try{Call("ConfigureAutostart",Launcher,Sid,store,true);}catch(InvalidOperationException){rejected=true;}
    Check(rejected&&store.updates==changes,"foreign tasks must never be rewritten");
    Console.WriteLine("PASS portable autostart ownership, current SID, migration, disable and idempotence");return 0;
  }catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}}
}
