using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

class UninstallTest {
  sealed class PreviewWindow:UninstallWindow {
    internal PreviewWindow(PanelUninstall.Plan plan,string language,Func<PanelUninstall.IOperations> create):base(plan,language,create){StartPosition=System.Windows.Forms.FormStartPosition.Manual;Location=new System.Drawing.Point(-32000,-32000);ShowInTaskbar=false;}
    protected override bool ShowWithoutActivation{get{return true;}}
    protected override System.Windows.Forms.CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
  }
  static int checks;
  static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
  static void Reject(Action action,string message){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,message);}
  static void Put(string file,string value){Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,value);}
  static string Manifest="{\"name\":\"quota-panel\",\"version\":\"1.7.6\",\"interface\":{\"displayName\":\"ChatGPT Limit Panel\"}}";
  static void Seal(Fixture fixture){var hashes=new Dictionary<string,string>();foreach(string file in Directory.GetFiles(fixture.Source,"*",SearchOption.AllDirectories))using(var sha=System.Security.Cryptography.SHA256.Create())hashes[file.Substring(fixture.Source.Length+1).Replace('\\','/')]=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant();Put(Path.Combine(fixture.Source,"uninstall-inventory.json"),new JavaScriptSerializer().Serialize(new{schema=1,pluginId="quota-panel@quota-panel-local",files=hashes}));}
  sealed class Fixture {
    internal string Root,Home,Local,Cache,Source,Auth,Other;
    internal Fixture(string parent,string name){
      Root=Path.Combine(parent,name);Home=Path.Combine(Root,"home");Local=Path.Combine(Root,"local");Cache=Path.Combine(Home,"plugins","cache","quota-panel-local","quota-panel","1.7.6");
      Put(Path.Combine(Cache,".codex-plugin","plugin.json"),Manifest);Put(Path.Combine(Cache,"dist","overlay","ChatGPTLimitPanelLauncher.exe"),"fixture");
      Source=Path.Combine(Home,"plugins","local-sources","chatgpt-limit-panel-owned");
      Put(Path.Combine(Source,".agents","plugins","marketplace.json"),"{\"name\":\"quota-panel-local\",\"plugins\":[{\"name\":\"quota-panel\",\"source\":{\"source\":\"local\",\"path\":\"./plugins/chatgpt-limit-panel\"}}]}");
      Put(Path.Combine(Source,"plugins","chatgpt-limit-panel",".codex-plugin","plugin.json"),Manifest);
      Auth=Path.Combine(Home,"auth.json");Other=Path.Combine(Home,"plugins","cache","other","another-plugin","1","keep.txt");Put(Auth,"credentials sentinel");Put(Other,"unrelated plugin");
      foreach(string folder in new[]{"ChatGPTLimitPanel","ChatGPTQuotaOverlay"})Put(Path.Combine(Local,folder,"settings.json"),"{\"themeMode\":\"light\"}");
    }
    internal PanelUninstall.Plan Plan(){return PanelUninstall.CreatePlan(Cache,Home,Local);}
  }
  sealed class Operations:PanelUninstall.IOperations {
    internal List<string> Calls=new List<string>();internal bool Foreign,FailRemoval,StillInstalled,Running;
    internal Action AfterVerify;
    public void Validate(PanelUninstall.Plan plan){Calls.Add("validate");if(Foreign)throw new Exception("foreign task");}
    public void StopMonitor(PanelUninstall.Plan plan){Calls.Add("stop");if(Running)throw new Exception("monitor did not stop");}
    public void RemoveTask(PanelUninstall.Plan plan){Calls.Add("task");}
    public void StopServers(PanelUninstall.Plan plan){Calls.Add("servers");}
    public void RemovePlugin(PanelUninstall.Plan plan){Calls.Add("plugin");if(FailRemoval)throw new Exception("native remove failed");PanelUninstall.DeleteOwnedTree(plan.CacheFamily,plan.CacheFamily);}
    public bool IsInstalled(PanelUninstall.Plan plan){Calls.Add("verify");if(AfterVerify!=null)AfterVerify();return StillInstalled;}
  }
  [STAThread]static void Main(string[] args){try{
    if(args.Length==6&&args[5]=="process-only"){var only=PanelUninstall.CreatePlan(args[1],args[2],args[3]);new PanelUninstall.NativeOperations(args[4],p=>{},p=>{},p=>{}).StopServers(only);Console.WriteLine("PASS isolated MCP process ownership");return;}
    Run(Path.GetFullPath(args[0]));if(args.Length==5){var plan=PanelUninstall.CreatePlan(args[1],args[2],args[3]);var result=PanelUninstall.Run(plan,true,new PanelUninstall.NativeOperations(args[4],p=>{},p=>{},p=>{}));Check(result.Complete&&!Directory.Exists(plan.CacheFamily),"real native CLI removal completes against the isolated installation");}Console.WriteLine("PASS uninstall ownership and sequence ("+checks+" assertions)");
  }catch(Exception error){Console.Error.WriteLine(error.Message);Environment.ExitCode=1;}}
  static void Run(string root){
    var keep=new Fixture(root,"keep");var plan=keep.Plan();Check(plan.PluginId=="quota-panel@quota-panel-local","derive the exact plugin identity from its installed cache");
    var ops=new Operations();var result=PanelUninstall.Run(plan,false,ops);
    Check(result.Complete,"successful removal reports completion");Check(String.Join(",",ops.Calls)=="validate,stop,task,servers,plugin,verify","validate before shutdown, remove task before cache, verify before cleanup");
    Check(!Directory.Exists(plan.CacheFamily)&&!Directory.Exists(keep.Source),"only the validated package and generated sources are removed");Check(File.Exists(Path.Combine(keep.Local,"ChatGPTLimitPanel","settings.json")),"preferences are preserved by default");Check(File.ReadAllText(keep.Auth)=="credentials sentinel"&&File.Exists(keep.Other),"sign-in and unrelated plugins survive");
    var full=new Fixture(root,"full");result=PanelUninstall.Run(full.Plan(),true,new Operations());Check(result.Complete&&!Directory.Exists(Path.Combine(full.Local,"ChatGPTLimitPanel"))&&!Directory.Exists(Path.Combine(full.Local,"ChatGPTQuotaOverlay")),"explicit full cleanup removes current and legacy panel data");Check(File.Exists(full.Auth)&&File.Exists(full.Other),"full cleanup preserves unrelated files");
    var foreign=new Fixture(root,"foreign");ops=new Operations{Foreign=true};Reject(()=>PanelUninstall.Run(foreign.Plan(),true,ops),"foreign task rejects removal");Check(ops.Calls.Count==1&&Directory.Exists(foreign.Cache),"ownership failure has no uninstall side effects");
    var busy=new Fixture(root,"busy");ops=new Operations{Running=true};Reject(()=>PanelUninstall.Run(busy.Plan(),true,ops),"a failed cooperative shutdown stops removal");Check(String.Join(",",ops.Calls)=="validate,stop"&&Directory.Exists(busy.Cache),"do not remove task or package while the monitor remains running");
    var failed=new Fixture(root,"failed");ops=new Operations{FailRemoval=true};Reject(()=>PanelUninstall.Run(failed.Plan(),true,ops),"native uninstall failure is not success");Check(Directory.Exists(failed.Source)&&File.Exists(Path.Combine(failed.Local,"ChatGPTLimitPanel","settings.json")),"failed native uninstall preserves sources and preferences");
    var lying=new Fixture(root,"lying");Reject(()=>PanelUninstall.Run(lying.Plan(),false,new Operations{StillInstalled=true}),"exit success alone cannot prove the plugin was removed");Check(Directory.Exists(lying.Source),"verification failure prevents later cleanup");
    var changed=new Fixture(root,"changed");plan=changed.Plan();Put(Path.Combine(changed.Cache,".codex-plugin","plugin.json"),Manifest.Replace("1.7.6","9.9.9"));ops=new Operations();Reject(()=>PanelUninstall.Run(plan,true,ops),"changed ownership receipt rejects a stale confirmation");Check(ops.Calls.Count==0,"receipt changes are detected before any action");
    var forged=new Fixture(root,"forged");Put(Path.Combine(forged.Cache,".codex-plugin","plugin.json"),Manifest.Replace("quota-panel","other-plugin"));Reject(()=>forged.Plan(),"same-shaped foreign manifests are rejected");
    var outside=new Fixture(root,"outside");Reject(()=>PanelUninstall.CreatePlan(outside.Home,outside.Home,outside.Local),"the home root cannot be a deletion target");Reject(()=>PanelUninstall.DeleteOwnedTree(outside.Home,outside.Cache),"an ancestor cannot replace an approved target");
    var shared=new Fixture(root,"shared");Put(Path.Combine(shared.Source,"keep.txt"),"user file");result=PanelUninstall.Run(shared.Plan(),false,new Operations());Check(Directory.Exists(shared.Source)&&File.Exists(Path.Combine(shared.Source,"keep.txt")),"a source with unrecognized contents is not treated as a generated copy");
    Check(!result.Complete,"preserved unknown source contents must be reported");
    var nested=new Fixture(root,"nested");string project=Path.Combine(nested.Source,"plugins","another-project","keep.txt");Put(project,"other project");result=PanelUninstall.Run(nested.Plan(),false,new Operations());Check(!result.Complete&&File.ReadAllText(project)=="other project","a nested second project is preserved and reported");
    var checkout=new Fixture(root,"checkout");string git=Path.Combine(checkout.Source,"plugins","chatgpt-limit-panel",".git","config");Put(git,"source checkout");result=PanelUninstall.Run(checkout.Plan(),false,new Operations());Check(!result.Complete&&File.Exists(git),"a checkout inside a generated-looking folder is never recursively removed");
    var locked=new Fixture(root,"locked");FileStream hold=null;try{ops=new Operations{AfterVerify=()=>{hold=new FileStream(Path.Combine(locked.Source,".agents","plugins","marketplace.json"),FileMode.Open,FileAccess.Read,FileShare.None);}};result=PanelUninstall.Run(locked.Plan(),false,ops);Check(!result.Complete&&Directory.Exists(locked.Source),"a previously approved source that becomes unreadable is reported, not silently skipped");}finally{if(hold!=null)hold.Dispose();}
    var receipt=new Fixture(root,"receipt");var hashes=new Dictionary<string,string>();foreach(string file in Directory.GetFiles(receipt.Source,"*",SearchOption.AllDirectories))using(var sha=System.Security.Cryptography.SHA256.Create())hashes[file.Substring(receipt.Source.Length+1).Replace('\\','/')]=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant();
    Put(Path.Combine(receipt.Source,"uninstall-inventory.json"),new JavaScriptSerializer().Serialize(new{schema=1,pluginId="quota-panel@quota-panel-local",files=hashes}));
    Put(Path.Combine(receipt.Source,"plugins","chatgpt-limit-panel",".codex-plugin","plugin.json"),Manifest.Replace("1.7.6","1.7.5"));result=PanelUninstall.Run(receipt.Plan(),false,new Operations());Check(!result.Complete&&Directory.Exists(receipt.Source),"modified known files fail the installer receipt and are preserved");
    var downgrade=new Fixture(root,"downgrade");Seal(downgrade);plan=downgrade.Plan();File.Delete(Path.Combine(downgrade.Source,"uninstall-inventory.json"));Put(Path.Combine(downgrade.Source,"plugins","chatgpt-limit-panel",".codex-plugin","plugin.json"),Manifest.Replace("1.7.6","1.7.5"));result=PanelUninstall.Run(plan,false,new Operations());Check(!result.Complete&&Directory.Exists(downgrade.Source),"removing a previously observed receipt cannot downgrade to legacy cleanup");
    var arriving=new Fixture(root,"arriving");var selected=PanelUninstall.SnapshotSource(arriving.Source,"quota-panel-local");Check(selected!=null,"capture a strict source inventory before cleanup");string added=Path.Combine(arriving.Source,"plugins","new-project","keep.txt");Put(added,"arrived after approval");Reject(()=>PanelUninstall.DeleteSourceInventory(selected),"new files make nonrecursive source cleanup incomplete");Check(File.ReadAllText(added)=="arrived after approval","files arriving after inventory validation are never deleted");
    var link=new Fixture(root,"link");string protectedFolder=Path.Combine(link.Root,"protected");Put(Path.Combine(protectedFolder,"keep.txt"),"outside sentinel");string redirected=Path.Combine(link.Local,"ChatGPTLimitPanel","redirected");
    bool redirectedCreated=CreateSymbolicLink(redirected,protectedFolder,3);
    if(!redirectedCreated){
      string script="New-Item -ItemType Junction -Path '"+redirected.Replace("'","''")+"' -Target '"+protectedFolder.Replace("'","''")+"' | Out-Null";
      using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script))){UseShellExecute=false,CreateNoWindow=true})){process.WaitForExit();redirectedCreated=process.ExitCode==0;}
    }
    Check(redirectedCreated,"create a redirected directory fixture without needing developer mode");
    if(redirectedCreated){
      result=PanelUninstall.Run(link.Plan(),true,new Operations());Check(!result.Complete&&File.Exists(Path.Combine(protectedFolder,"keep.txt")),"redirected data causes partial-cleanup reporting and never follows the link");
    }
    string[] good={"\"C:\\owned\\runtime\\node.exe\" ./server/index.mjs","\"C:\\owned\\runtime\\node.exe\" C:\\owned\\server\\mcp.mjs"};
    foreach(string command in good)Check(PanelUninstall.IsServerCommand(command,@"C:\owned\runtime\node.exe",@"C:\owned",@"C:\owned"),"recognize only the own MCP entry points with a verified cwd");
    Check(!PanelUninstall.IsServerCommand(good[0],@"C:\owned\runtime\node.exe",@"C:\owned"),"unknown cwd cannot authorize a relative script");
    Check(!PanelUninstall.IsServerCommand(good[0],@"C:\owned\runtime\node.exe",@"C:\owned",@"C:\other-project"),"another project's matching relative script is not the MCP server");
    using(var current=System.Diagnostics.Process.GetCurrentProcess())Check(PanelUninstall.WorkingDirectory(current)==PanelUninstall.Full(Directory.GetCurrentDirectory()),"actual native current-directory acquisition matches this x64 process");
    foreach(string command in new[]{"node.exe other.mjs","node.exe ./server/index.mjs extra","node.exe ./server/index.mjs.backup","node.exe --eval stop"})Check(!PanelUninstall.IsServerCommand(command,@"C:\owned\runtime\node.exe",@"C:\owned"),"reject unrelated or ambiguous node command lines");
    var ui=new Fixture(root,"ui");var untouched=new Operations();
    IntPtr foreground=GetForegroundWindow();
    foreach(string language in new[]{"ru","en"})using(var window=new PreviewWindow(ui.Plan(),language,()=>untouched)){
      window.Show();System.Windows.Forms.Application.DoEvents();Check(GetForegroundWindow()==foreground,"offscreen UI verification must preserve the user's foreground window");
      Check(!window.DeleteSettings.Checked,"settings removal requires explicit selection");
      string output=Environment.GetEnvironmentVariable("QUOTA_UNINSTALL_ARTIFACT_DIR");if(!String.IsNullOrEmpty(output)){Directory.CreateDirectory(output);using(var bitmap=new System.Drawing.Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,window.Width,window.Height));bitmap.Save(Path.Combine(output,"uninstall-"+language+".png"));}}
      window.CancelRemoval.PerformClick();Check(untouched.Calls.Count==0&&Directory.Exists(ui.Cache),"opening and canceling confirmation never invokes removal");
    }
  }
  [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
  [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.I1)]static extern bool CreateSymbolicLink(string link,string target,int flags);
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
}
