using System;
using System.Reflection;
using System.Xml;
class OverlayAutostartTest {
  const string Sid="S-1-5-21-100-200-300-1001",Launcher=@"C:\Users\Другой User\.codex\plugins\cache\market\quota-panel\0.2.0-beta.3\dist\overlay\ChatGPTLimitPanelLauncher.exe";
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  static MethodInfo Method(string name,int count){foreach(var m in typeof(QuotaPanelTaskHost).GetMethods(BindingFlags.Static|BindingFlags.NonPublic))if(m.Name==name&&m.GetParameters().Length==count)return m;throw new Exception("missing portable autostart operation "+name);}
  static object Call(string name,params object[] args){try{return Method(name,args.Length).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
  static string Value(string xml,string query){var doc=new XmlDocument();doc.LoadXml(xml);var ns=new XmlNamespaceManager(doc.NameTable);ns.AddNamespace("t",doc.DocumentElement.NamespaceURI);var node=doc.SelectSingleNode(query,ns);return node==null?null:node.InnerText;}
  class Store:QuotaPanelTaskHost.ITaskStore {
    public QuotaPanelTaskHost.TaskRecord record;public int creates,updates,runs,reads;public Action onRead;public bool failUpdate;
    public QuotaPanelTaskHost.TaskRecord Read(string name){reads++;if(onRead!=null)onRead();return record;}
    public void Register(string name,string xml,string sid){Check(record==null,"create must not overwrite");creates++;record=new QuotaPanelTaskHost.TaskRecord {xml=xml,enabled=true};}
    public void Update(string name,string xml,string sid){if(failUpdate)throw new System.IO.IOException("denied");Check(record!=null,"update requires an existing own record");updates++;record.xml=xml;}
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
    store.record.xml=xml.Replace("ChatGPTLimitPanelLauncher.exe","other.exe");int changes=store.updates;
    bool rejected=false;try{Call("ConfigureAutostart",Launcher,Sid,store,true);}catch(InvalidOperationException){rejected=true;}
    Check(rejected&&store.updates==changes,"foreign tasks must never be rewritten");
    // A removed old package need not exist on disk; the owned task is the migration receipt.
    string next=Launcher.Replace("0.2.0-beta.3","1.6.1");
    var migration=new Store();Call("ConfigureAutostart",Launcher,Sid,migration,false);migration.record.enabled=false;migration.record.xml=migration.record.xml.Replace("<Enabled>true</Enabled>","<Enabled>false</Enabled>");
    int writes=migration.updates;string oldXml=migration.record.xml;
    Check(QuotaPanelTaskHost.Read(next,Sid,migration).installed&&migration.updates==writes,"new status must recognize previous owned install without changing it");
    Call("ConfigureAutostart",next,Sid,migration,false);
    Check(migration.updates==writes+1&&migration.creates==1&&migration.runs==0,"upgrade must update the existing task without removing, recreating or starting it");
    Check(Value(migration.record.xml,"/t:Task/t:Actions/t:Exec/t:Command")==next&&Value(migration.record.xml,"/t:Task/t:Actions/t:Exec/t:WorkingDirectory")==System.IO.Path.GetDirectoryName(next),"command and working directory migrate together");
    Check(!migration.record.enabled&&!QuotaPanelTaskHost.Read(next,Sid,migration).autoStartEnabled,"both disabled flags survive upgrade");
    Check(migration.record.xml==oldXml.Replace("0.2.0-beta.3","1.6.1"),"only executable and working directory paths may change");
    writes=migration.updates;Call("ConfigureAutostart",next,Sid,migration,false);Check(migration.updates==writes,"repeat upgrade is idempotent");
    migration.record=new QuotaPanelTaskHost.TaskRecord {xml=xml,running=true,enabled=true};writes=migration.updates;
    bool busy=false;try{Call("ConfigureAutostart",next,Sid,migration,true);}catch(InvalidOperationException){busy=true;}
    Check(busy&&migration.updates==writes&&migration.record.xml==xml,"running predecessor must remain untouched");
    foreach(string foreign in new[]{xml.Replace("LeastPrivilege","HighestAvailable"),xml.Replace(Sid,"S-1-5-21-9-9-9-1001"),xml.Replace("ChatGPTLimitPanelLauncher.exe","Other.exe"),xml.Replace("--run-host","--start"),xml.Replace("\\dist\\overlay\\","\\other\\"),xml.Replace("Quota Panel per-user logon host v2","Other task")}) {
      migration.record=new QuotaPanelTaskHost.TaskRecord {xml=foreign,enabled=true};writes=migration.updates;
      bool blocked=false;try{Call("ConfigureAutostart",next,Sid,migration,true);}catch(InvalidOperationException){blocked=true;}
      Check(blocked&&migration.updates==writes&&migration.record.xml==foreign,"unverified predecessor must never be adopted");
    }
    var legacyMove=new Store {record=new QuotaPanelTaskHost.TaskRecord {xml=QuotaPanelTaskHost.CreateTaskXml(Launcher,Sid),enabled=true}};
    QuotaPanelTaskHost.Ensure(next,Sid,legacyMove);
    Check(legacyMove.updates==1&&Value(legacyMove.record.xml,"/t:Task/t:Triggers")==""&&QuotaPanelTaskHost.IsOwnTaskXml(legacyMove.record.xml,next,Sid),"legacy task can move without introducing logon triggers");
    var oldBrand=new Store {record=new QuotaPanelTaskHost.TaskRecord {xml=xml.Replace("ChatGPTLimitPanelLauncher.exe","QuotaMonitor.exe"),enabled=true}};
    Call("ConfigureAutostart",next,Sid,oldBrand,true);
    Check(oldBrand.updates==1&&QuotaPanelTaskHost.IsOwnTaskXml(oldBrand.record.xml,next,Sid),"old published launcher name migrates with the same strict SID and task validation");
    var denied=new Store {record=new QuotaPanelTaskHost.TaskRecord {xml=xml,enabled=true},failUpdate=true};
    bool failed=false;try{Call("ConfigureAutostart",next,Sid,denied,true);}catch(System.IO.IOException){failed=true;}
    Check(failed&&denied.record.xml==xml&&denied.creates==0&&denied.runs==0,"denied update must leave the predecessor intact");
    var race=new Store {record=new QuotaPanelTaskHost.TaskRecord {xml=xml,enabled=true}};
    race.onRead=delegate{if(race.reads==2)race.record=new QuotaPanelTaskHost.TaskRecord {xml=xml.Replace("--run-host","--other"),enabled=true};};
    bool changed=false;try{Call("ConfigureAutostart",next,Sid,race,true);}catch(InvalidOperationException){changed=true;}
    Check(changed&&race.updates==0,"a definition replaced during validation must not be overwritten");
    Console.WriteLine("PASS portable autostart ownership, current SID, migration, disable and idempotence");return 0;
  }catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}}
}
