using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// The confirmation helper is copied outside the plugin before it is opened.
// Opening it is read-only. Only an explicit confirmation calls Run.
internal static class PanelUninstall {
  internal sealed class Plan {
    internal string Root,Home,Local,PluginId,Marketplace,Version,Launcher,CacheFamily,Receipt;
    internal readonly List<string> Sources=new List<string>();
    internal readonly HashSet<string> ProtectedSources=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<string,string> SourceReceipts=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  }
  internal sealed class Outcome {
    internal readonly List<string> Remaining=new List<string>();
    internal bool Complete {get{return Remaining.Count==0;}}
  }
  internal interface IOperations {
    void Validate(Plan plan);
    void StopMonitor(Plan plan);
    void RemoveTask(Plan plan);
    void StopServers(Plan plan);
    void RemovePlugin(Plan plan);
    bool IsInstalled(Plan plan);
  }
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
  internal static string Full(string path) {
    if(String.IsNullOrWhiteSpace(path))throw new IOException("Missing local path");
    if(path.StartsWith(@"\\?\",StringComparison.Ordinal))path=path.Substring(4);
    if(!Path.IsPathRooted(path)||path.StartsWith(@"\\",StringComparison.Ordinal))throw new IOException("A local absolute path is required");
    string full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
    if(File.Exists(full)||Directory.Exists(full)){var buffer=new StringBuilder(32768);uint length=GetLongPathName(full,buffer,buffer.Capacity);if(length>0&&length<buffer.Capacity)full=buffer.ToString().TrimEnd(Path.DirectorySeparatorChar);}
    return full;
  }
  static bool Same(string a,string b){return String.Equals(Full(a),Full(b),StringComparison.OrdinalIgnoreCase);}
  static bool Inside(string path,string parent){return Full(path).StartsWith(Full(parent)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
  static Dictionary<string,object> Read(string file){return Json.DeserializeObject(File.ReadAllText(file)) as Dictionary<string,object>??new Dictionary<string,object>();}
  static string Value(Dictionary<string,object> data,string key){object value;return data.TryGetValue(key,out value)?Convert.ToString(value,CultureInfo.InvariantCulture):"";}
  static bool OwnManifest(string root,string version=null){
    var manifest=Read(Path.Combine(root,".codex-plugin","plugin.json"));object value;
    var ui=manifest.TryGetValue("interface",out value)?value as Dictionary<string,object>:null;
    return Value(manifest,"name")=="quota-panel"&&ui!=null&&Value(ui,"displayName")=="ChatGPT Limit Panel"&&(version==null||Value(manifest,"version")==version);
  }
  static void PlainPath(string path,string boundary){
    string current=Full(path),limit=Full(boundary);
    if(!Same(current,limit)&&!Inside(current,limit))throw new IOException("Path is outside its approved root");
    while(true){if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Redirected paths are not removed");if(Same(current,limit))break;current=Path.GetDirectoryName(current);}
  }
  static void PlainTree(string root){
    if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("Redirected paths are not removed");
    foreach(string entry in Directory.GetFileSystemEntries(root)){var attributes=File.GetAttributes(entry);if((attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Redirected paths are not removed");if((attributes&FileAttributes.Directory)!=0)PlainTree(entry);}
  }
  internal static Plan CreatePlan(string root,string home,string local){
    var plan=new Plan{Root=Full(root),Home=Full(home),Local=Full(local)};
    string cache=Path.Combine(plan.Home,"plugins","cache");PlainPath(plan.Root,plan.Home);
    if(!Inside(plan.Root,cache)||!Directory.Exists(plan.Root))throw new IOException("The plugin must be installed in its own cache");
    string[] pieces=plan.Root.Substring(Full(cache).Length+1).Split(Path.DirectorySeparatorChar);
    if(pieces.Length!=3||pieces[1]!="quota-panel"||!System.Text.RegularExpressions.Regex.IsMatch(pieces[0],@"^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$")||!System.Text.RegularExpressions.Regex.IsMatch(pieces[2],@"^\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?$"))throw new IOException("Unknown plugin cache layout");
    plan.Marketplace=pieces[0];plan.Version=pieces[2];plan.PluginId="quota-panel@"+plan.Marketplace;plan.CacheFamily=Path.GetDirectoryName(plan.Root);plan.Launcher=Path.Combine(plan.Root,"dist","overlay","ChatGPTLimitPanelLauncher.exe");
    if(!OwnManifest(plan.Root,plan.Version)||!File.Exists(plan.Launcher))throw new IOException("The package is not ChatGPT Limit Panel");
    PlainTree(plan.CacheFamily);
    using(var hash=SHA256.Create())plan.Receipt=Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(Path.Combine(plan.Root,".codex-plugin","plugin.json"))));
    string sources=Path.Combine(plan.Home,"plugins","local-sources");
    if(Directory.Exists(sources)){PlainPath(sources,plan.Home);foreach(string candidate in Directory.GetDirectories(sources,"chatgpt-limit-panel-*"))if(SourceIdentity(candidate,plan.Marketplace)){
      string source=Full(candidate);plan.Sources.Add(source);try{plan.SourceReceipts[source]=SourceReceipt(source);}catch{plan.SourceReceipts[source]="unreadable";}
    }}
    return plan;
  }
  static bool SourceIdentity(string root,string marketplace){try{
    if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)return false;
    PlainPath(Path.Combine(root,".agents","plugins","marketplace.json"),root);PlainPath(Path.Combine(root,"plugins","chatgpt-limit-panel",".codex-plugin","plugin.json"),root);
    var catalog=Read(Path.Combine(root,".agents","plugins","marketplace.json"));object rows;
    var plugins=catalog.TryGetValue("plugins",out rows)?rows as object[]:null;
    if(Value(catalog,"name")!=marketplace||plugins==null||plugins.Length!=1)return false;
    var plugin=plugins[0] as Dictionary<string,object>;object source;
    var location=plugin!=null&&plugin.TryGetValue("source",out source)?source as Dictionary<string,object>:null;
    return plugin!=null&&Value(plugin,"name")=="quota-panel"&&location!=null&&Value(location,"source")=="local"&&Value(location,"path").Replace('\\','/')=="./plugins/chatgpt-limit-panel"&&OwnManifest(Path.Combine(root,"plugins","chatgpt-limit-panel"));
  }catch{return false;}}
  static readonly string[] SourcePayload={"mcp.json",".mcp.json",".codex-plugin/plugin.json","assets/chatgpt-limit-panel.svg","assets/chatgpt-limit-panel.png","server/index.mjs","server/mcp.mjs","server/overlay-controller.mjs","server/ui.mjs","server/language.mjs","server/updates.mjs","server/uninstall.mjs","ui/control.html","ui/control.mjs","overlay/host.mjs","overlay/model.mjs","plugins/chatgpt-limit-panel/server/reader.mjs","plugins/chatgpt-limit-panel/server/limits.mjs","plugins/chatgpt-limit-panel/server/updates.mjs","dist/overlay/ChatGPTLimitPanel.exe","dist/overlay/ChatGPTLimitPanelLauncher.exe","dist/overlay/ChatGPTLimitPanelUninstaller.exe","dist/overlay/QuotaMonitor.exe","dist/overlay/runtime.json","runtime/node.exe","runtime/NODE-LICENSE.txt","README.txt","README.md","LICENSE.txt","LICENSE","UPGRADING.txt","UPGRADING.md","UNINSTALL.txt","UNINSTALL.md"};
  internal sealed class SourceInventory {internal string Root;internal readonly Dictionary<string,string> Files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);internal readonly List<string> Directories=new List<string>();}
  static string HashFile(string file){using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
  static string SourceReceipt(string root){string file=Path.Combine(root,"uninstall-inventory.json");try{var attributes=File.GetAttributes(file);if((attributes&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)throw new IOException("Invalid source receipt");return HashFile(file);}catch(FileNotFoundException){return "absent";}catch(DirectoryNotFoundException){return "absent";}}
  static bool GeneratedSource(string root,string marketplace){return SnapshotSource(root,marketplace)!=null;}
  internal static SourceInventory SnapshotSource(string root,string marketplace,string expectedReceipt=null){try{
    string marker=SourceReceipt(root);if(expectedReceipt!=null&&marker!=expectedReceipt)return null;
    if(!SourceIdentity(root,marketplace))return null;PlainTree(root);
    var snapshot=new SourceInventory{Root=root};
    var allowed=new HashSet<string>(SourcePayload.Select(file=>"plugins/chatgpt-limit-panel/"+file),StringComparer.OrdinalIgnoreCase);allowed.Add(".agents/plugins/marketplace.json");allowed.Add("config-before-install.toml");
    string receipt=Path.Combine(root,"uninstall-inventory.json");Dictionary<string,object> hashes=null;
    if(marker!="absent"){
      var inventory=Read(receipt);object value;
      if(Value(inventory,"schema")!="1"||Value(inventory,"pluginId")!="quota-panel@"+marketplace||!inventory.TryGetValue("files",out value)||(hashes=value as Dictionary<string,object>)==null)return null;
      if(hashes.Keys.Any(file=>!allowed.Contains(file)))return null;allowed.Add("uninstall-inventory.json");
    }
    var files=new List<string>();var folders=new Stack<string>();folders.Push(root);
    while(folders.Count>0)foreach(string entry in Directory.GetFileSystemEntries(folders.Pop())){
      string relative=entry.Substring(root.Length+1).Replace('\\','/');var attributes=File.GetAttributes(entry);if((attributes&FileAttributes.ReparsePoint)!=0)return null;
      if((attributes&FileAttributes.Directory)!=0){if(!allowed.Any(file=>file.StartsWith(relative+"/",StringComparison.OrdinalIgnoreCase)))return null;folders.Push(entry);snapshot.Directories.Add(entry);}
      else {if(!allowed.Contains(relative))return null;snapshot.Files[entry]=HashFile(entry);if(relative!="uninstall-inventory.json")files.Add(relative);}
    }
    if(hashes!=null){if(files.Count!=hashes.Count)return null;foreach(string file in files){object hash;if(!hashes.TryGetValue(file,out hash)||snapshot.Files[Path.Combine(root,file.Replace('/',Path.DirectorySeparatorChar))]!=Convert.ToString(hash))return null;}}
    if(SourceReceipt(root)!=marker)return null;return snapshot;
  }catch{return null;}}
  internal static void DeleteSourceInventory(SourceInventory snapshot){
    PlainPath(snapshot.Root,Path.GetDirectoryName(snapshot.Root));
    foreach(var entry in snapshot.Files){PlainPath(entry.Key,snapshot.Root);if(HashFile(entry.Key)!=entry.Value)throw new IOException("Source files changed after verification");}
    // Never enumerate recursively after approval: a newly added user file must
    // survive and make non-recursive directory removal report a leftover.
    foreach(var entry in snapshot.Files){PlainPath(entry.Key,snapshot.Root);if(HashFile(entry.Key)!=entry.Value)throw new IOException("Source files changed after verification");File.Delete(entry.Key);}
    foreach(string folder in snapshot.Directories.OrderByDescending(path=>path.Length)){PlainPath(folder,snapshot.Root);Directory.Delete(folder,false);}
    Directory.Delete(snapshot.Root,false);
  }
  internal static Outcome Run(Plan original,bool deleteSettings,IOperations operations){
    var plan=CreatePlan(original.Root,original.Home,original.Local);
    if(plan.Receipt!=original.Receipt)throw new IOException("The installation changed; open removal again");
    foreach(string source in original.Sources)if(!plan.Sources.Contains(source))plan.Sources.Add(source);
    foreach(var entry in original.SourceReceipts)plan.SourceReceipts[entry.Key]=entry.Value;
    operations.Validate(plan);operations.StopMonitor(plan);operations.RemoveTask(plan);operations.StopServers(plan);operations.RemovePlugin(plan);
    if(operations.IsInstalled(plan)||Directory.Exists(plan.CacheFamily))throw new IOException("The native plugin removal could not be verified");
    var outcome=new Outcome();
    foreach(string source in plan.Sources){
      try{
        if(!DirectoryPresent(source))continue;string receipt;plan.SourceReceipts.TryGetValue(source,out receipt);
        var snapshot=plan.ProtectedSources.Contains(source)?null:SnapshotSource(source,plan.Marketplace,receipt);
        if(snapshot==null){outcome.Remaining.Add(source);continue;}DeleteSourceInventory(snapshot);
      }catch{outcome.Remaining.Add(source);}
    }
    if(deleteSettings)foreach(string name in new[]{"ChatGPTLimitPanel","ChatGPTQuotaOverlay"}){
      string folder=Path.Combine(plan.Local,name);try{if(DirectoryPresent(folder)){PlainPath(folder,plan.Local);DeleteOwnedTree(folder,folder);}}catch{outcome.Remaining.Add(folder);}
    }
    return outcome;
  }
  static bool DirectoryPresent(string path){try{if((File.GetAttributes(path)&FileAttributes.Directory)==0)throw new IOException("Expected a directory");return true;}catch(FileNotFoundException){return false;}catch(DirectoryNotFoundException){return false;}}
  internal static void DeleteOwnedTree(string target,string approved){
    if(!Same(target,approved))throw new IOException("Deletion target differs from the approved folder");
    if(!DirectoryPresent(target))return;PlainPath(target,Path.GetDirectoryName(target));PlainTree(target);DeletePlainTree(target);
  }
  static void DeletePlainTree(string root){
    if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("Redirected paths are not removed");
    foreach(string entry in Directory.GetFileSystemEntries(root)){
      var attributes=File.GetAttributes(entry);if((attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Redirected paths are not removed");
      if((attributes&FileAttributes.Directory)!=0)DeletePlainTree(entry);else File.Delete(entry);
    }
    Directory.Delete(root,false);
  }
  internal static bool IsServerCommand(string command,string executable,string root,string workingDirectory=null){
    IntPtr argv=IntPtr.Zero;try{
      int count;argv=CommandLineToArgvW(command,out count);if(argv==IntPtr.Zero||count!=2)return false;
      string first=Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv)),argument=Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv,IntPtr.Size));
      if(!Path.IsPathRooted(first)||!Same(first,executable))return false;
      string entry=argument;
      if(!Path.IsPathRooted(entry)){if(String.IsNullOrEmpty(workingDirectory))return false;entry=Path.Combine(workingDirectory,entry.Replace('/',Path.DirectorySeparatorChar));}
      return Same(entry,Path.Combine(root,"server","index.mjs"))||Same(entry,Path.Combine(root,"server","mcp.mjs"));
    }catch{return false;}finally{if(argv!=IntPtr.Zero)LocalFree(argv);}
  }
  [StructLayout(LayoutKind.Sequential)]struct BasicInformation {internal IntPtr Reserved1,Peb,Reserved2,Reserved3,Pid,Reserved4;}
  [StructLayout(LayoutKind.Sequential)]struct PebPrefix {internal byte Inherited,ReadOptions,Debugged,Flags;internal IntPtr Mutant,ImageBase,Loader,Parameters;}
  [StructLayout(LayoutKind.Sequential)]struct UnicodeString {internal ushort Length,Maximum;internal IntPtr Buffer;}
  [StructLayout(LayoutKind.Sequential)]struct ParametersPrefix {internal uint MaximumLength,Length,Flags,DebugFlags;internal IntPtr Console;internal uint ConsoleFlags;internal IntPtr Input,Output,Error;internal UnicodeString CurrentDirectory;internal IntPtr DirectoryHandle;}
  static T Remote<T>(IntPtr handle,IntPtr address) where T:struct {
    byte[] bytes=new byte[Marshal.SizeOf(typeof(T))];IntPtr read;
    if(address==IntPtr.Zero||!ReadProcessMemory(handle,address,bytes,new IntPtr(bytes.Length),out read)||read.ToInt64()!=bytes.Length)throw new IOException("Process parameters are unavailable");
    var pinned=GCHandle.Alloc(bytes,GCHandleType.Pinned);try{return (T)Marshal.PtrToStructure(pinned.AddrOfPinnedObject(),typeof(T));}finally{pinned.Free();}
  }
  internal static string WorkingDirectory(Process process){try{
    // Read-only x64 process parameters; failure is never permission to stop a PID.
    // Prefix layouts: Microsoft winternl PEB documentation and System Informer phnt.
    // https://learn.microsoft.com/windows/win32/api/winternl/ns-winternl-peb
    // https://github.com/winsiderss/systeminformer/tree/master/phnt/include
    bool wow64;if(IntPtr.Size!=8||!IsWow64Process(process.Handle,out wow64)||wow64)return null;
    BasicInformation basic;int returned;if(NtQueryInformationProcess(process.Handle,0,out basic,Marshal.SizeOf(typeof(BasicInformation)),out returned)<0)return null;
    var peb=Remote<PebPrefix>(process.Handle,basic.Peb);var parameters=Remote<ParametersPrefix>(process.Handle,peb.Parameters);var directory=parameters.CurrentDirectory;
    if(parameters.Length<Marshal.SizeOf(typeof(ParametersPrefix))||directory.Length==0||directory.Length%2!=0||directory.Length>65532||directory.Maximum<directory.Length||directory.Buffer==IntPtr.Zero)return null;
    byte[] bytes=new byte[directory.Length];IntPtr read;if(!ReadProcessMemory(process.Handle,directory.Buffer,bytes,new IntPtr(bytes.Length),out read)||read.ToInt64()!=bytes.Length)return null;
    return Full(Encoding.Unicode.GetString(bytes));
  }catch{return null;}}
  internal sealed class NativeOperations:IOperations {
    readonly string codex;readonly Action<Plan> taskValidation,monitorStop,taskRemoval;string generatedMarketplace;
    internal NativeOperations(string executable,Action<Plan> validate=null,Action<Plan> stop=null,Action<Plan> removeTask=null){codex=Full(executable);taskValidation=validate;monitorStop=stop;taskRemoval=removeTask;}
    string Execute(Plan plan,string executable,string arguments,int timeout){
      var start=new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetTempPath()};start.EnvironmentVariables["CODEX_HOME"]=plan.Home;
      using(var process=Process.Start(start)){
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(timeout)){try{process.Kill();}catch{}throw new IOException("The uninstall command timed out");}
        Task.WaitAll(new Task[]{output,error},2000);if(process.ExitCode!=0||!output.IsCompleted||!error.IsCompleted)throw new IOException("The uninstall command failed");return output.Result.Trim().TrimStart('\uFEFF');
      }
    }
    Dictionary<string,object>[] Installed(Plan plan){
      var data=Json.DeserializeObject(Execute(plan,codex,"plugin list --json",15000)) as Dictionary<string,object>;object rows;
      var values=data!=null&&data.TryGetValue("installed",out rows)?rows as object[]:null;
      if(values==null)throw new IOException("Plugin registration could not be read");return values.OfType<Dictionary<string,object>>().ToArray();
    }
    public void Validate(Plan plan){
      var installed=Installed(plan);var own=installed.FirstOrDefault(row=>Value(row,"pluginId")==plan.PluginId&&Value(row,"version")==plan.Version&&Value(row,"installed")=="True");
      if(own==null)throw new IOException("The installed plugin changed; open removal again");
      foreach(var other in installed.Where(row=>Value(row,"pluginId")!=plan.PluginId)){
        object raw;var marketplace=other.TryGetValue("marketplaceSource",out raw)?raw as Dictionary<string,object>:null;
        if(marketplace!=null)try{string referenced=Full(Value(marketplace,"source"));foreach(string candidate in plan.Sources)if(Same(candidate,referenced)||Inside(referenced,candidate))plan.ProtectedSources.Add(candidate);}catch{}
      }
      object item;var source=own.TryGetValue("marketplaceSource",out item)?item as Dictionary<string,object>:null;
      if(source!=null)try{string path=Full(Value(source,"source"));if(plan.Sources.Any(candidate=>Same(candidate,path)&&!plan.ProtectedSources.Contains(candidate)&&GeneratedSource(candidate,plan.Marketplace))&&!installed.Any(row=>Value(row,"marketplaceName")==plan.Marketplace&&Value(row,"pluginId")!=plan.PluginId))generatedMarketplace=plan.Marketplace;}catch{}
      if(taskValidation!=null)taskValidation(plan);else {
        string taskLauncher=QuotaPanelTaskHost.ValidateRemoval(plan.Launcher);
        if(taskLauncher!=null&&!Same(taskLauncher,plan.Launcher)){
          string oldRoot=Full(Path.Combine(Path.GetDirectoryName(taskLauncher),"..",".."));PlainPath(taskLauncher,plan.Home);
          if(!File.Exists(taskLauncher)||!OwnManifest(oldRoot,Path.GetFileName(oldRoot)))throw new IOException("The launch task is not a verified version of this installation");
        }
      }
    }
    public void StopMonitor(Plan plan){
      if(monitorStop!=null){monitorStop(plan);return;}
      var state=Json.DeserializeObject(Execute(plan,plan.Launcher,"--stop-json",10000)) as Dictionary<string,object>;
      if(state==null||Value(state,"running")!="False"||Value(state,"status")!="stopped")throw new IOException("The monitor did not stop");
    }
    public void RemoveTask(Plan plan){
      if(taskRemoval!=null){taskRemoval(plan);return;}
      var timer=Stopwatch.StartNew();while(QuotaPanelTaskHost.Read(plan.Launcher).running){if(timer.ElapsedMilliseconds>5000)throw new IOException("The panel host has not stopped");Thread.Sleep(50);}
      QuotaPanelTaskHost.RemoveOwn(plan.Launcher);
    }
    public void StopServers(Plan plan){
      var allowed=new List<string>();foreach(string version in Directory.GetDirectories(plan.CacheFamily))try{PlainPath(version,plan.Home);if(OwnManifest(version,Path.GetFileName(version)))allowed.Add(Full(version));}catch{}
      using(var search=new ManagementObjectSearcher("SELECT ProcessId,ExecutablePath,CommandLine,CreationDate FROM Win32_Process WHERE Name = 'node.exe'"))using(var records=search.Get())foreach(ManagementObject record in records){
        try{
          string executable=Convert.ToString(record["ExecutablePath"]),command=Convert.ToString(record["CommandLine"]);if(String.IsNullOrEmpty(executable))continue;
          string owner=allowed.FirstOrDefault(root=>Same(executable,Path.Combine(root,"runtime","node.exe")));if(owner==null)continue;
          int pid=Convert.ToInt32(record["ProcessId"]);using(var process=Process.GetProcessById(pid)){
            // Bind this Process object to the same live handle before checking
            // image, birth time and cwd, preventing PID reuse between checks.
            if(process.Handle==IntPtr.Zero||!Same(process.MainModule.FileName,executable))continue;
            DateTime observed=ManagementDateTimeConverter.ToDateTime(Convert.ToString(record["CreationDate"])).ToUniversalTime();
            if(Math.Abs((process.StartTime.ToUniversalTime()-observed).TotalMilliseconds)>10||!IsServerCommand(command,executable,owner,WorkingDirectory(process)))continue;
            process.Kill();if(!process.WaitForExit(3000))throw new IOException("A plugin server did not close");
          }
        }catch(ArgumentException){}catch(InvalidOperationException){}finally{record.Dispose();}
      }
    }
    public void RemovePlugin(Plan plan){
      Execute(plan,codex,"plugin remove "+Quote(plan.PluginId)+" --json",20000);
      if(generatedMarketplace!=null)Execute(plan,codex,"plugin marketplace remove "+Quote(generatedMarketplace)+" --json",15000);
    }
    public bool IsInstalled(Plan plan){return Installed(plan).Any(row=>Value(row,"pluginId")==plan.PluginId&&Value(row,"installed")=="True");}
  }
  internal static string Quote(string argument){
    var result=new StringBuilder("\"");int slashes=0;
    foreach(char value in argument){if(value=='\\'){slashes++;continue;}if(value=='\"')result.Append('\\',slashes*2+1);else result.Append('\\',slashes);slashes=0;result.Append(value);}
    result.Append('\\',slashes*2);return result.Append('"').ToString();
  }
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetLongPathName(string path,StringBuilder result,int length);
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CommandLineToArgvW(string command,out int count);
  [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr pointer);
  [DllImport("kernel32.dll")]static extern bool ReadProcessMemory(IntPtr process,IntPtr address,[Out]byte[] buffer,IntPtr size,out IntPtr read);
  [DllImport("kernel32.dll")]static extern bool IsWow64Process(IntPtr process,out bool wow64);
  [DllImport("ntdll.dll")]static extern int NtQueryInformationProcess(IntPtr process,int informationClass,out BasicInformation value,int size,out int returned);
}

internal class UninstallWindow:Form {
  internal readonly CheckBox DeleteSettings;
  internal readonly Button Confirm,CancelRemoval;
  internal readonly Label Status;
  bool running,finished;readonly bool english;readonly PanelUninstall.Plan plan;readonly Func<PanelUninstall.IOperations> operations;
  internal UninstallWindow(PanelUninstall.Plan value,string language,Func<PanelUninstall.IOperations> create){
    plan=value;operations=create;english=language=="en";Text="ChatGPT Limit Panel";ClientSize=new Size(570,322);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;
    BackColor=Color.FromArgb(38,40,43);ForeColor=Color.FromArgb(237,237,239);Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
    var heading=new Label{Text=english?"Remove ChatGPT Limit Panel?":"Удалить ChatGPT Limit Panel?",Font=new Font("Segoe UI",14,FontStyle.Bold),Location=new Point(24,22),Size=new Size(520,36)};
    var explanation=new Label{Text=english?"The helper will stop the panel, remove its Windows launch task,\nand uninstall the plugin and its verified package copies.\nYour Codex sign-in and other plugins will be kept.":"Помощник остановит панель, уберёт её задачу запуска Windows,\nудалит плагин и проверенные копии его пакета.\nВход в Codex и другие плагины сохранятся.",Location=new Point(24,70),Size=new Size(522,76)};
    DeleteSettings=new CheckBox{Text=english?"Also delete settings, saved positions and logs":"Также удалить настройки, положения и журналы",Location=new Point(24,154),Size=new Size(525,28),Checked=false,AccessibleName=english?"Delete panel settings":"Удалить настройки панели"};
    Status=new Label{Text=english?"Settings are kept unless you select the checkbox.":"Без галочки настройки сохранятся.",Location=new Point(24,193),Size=new Size(522,54),ForeColor=Color.FromArgb(185,188,193)};
    Confirm=new Button{Text=english?"Remove plugin":"Удалить плагин",Location=new Point(268,265),Size=new Size(148,34),BackColor=Color.FromArgb(137,55,59),ForeColor=ForeColor,FlatStyle=FlatStyle.Flat};
    CancelRemoval=new Button{Text=english?"Cancel":"Отмена",Location=new Point(428,265),Size=new Size(118,34),BackColor=BackColor,ForeColor=ForeColor,FlatStyle=FlatStyle.Flat};
    Controls.AddRange(new Control[]{heading,explanation,DeleteSettings,Status,Confirm,CancelRemoval});CancelButton=CancelRemoval;
    CancelRemoval.Click+=delegate{Close();};Confirm.Click+=async delegate{await ConfirmRemoval();};FormClosing+=delegate(object sender,FormClosingEventArgs args){if(running)args.Cancel=true;};
  }
  async Task ConfirmRemoval(){
    if(running||finished)return;running=true;Confirm.Enabled=false;CancelRemoval.Enabled=false;DeleteSettings.Enabled=false;bool remove=DeleteSettings.Checked;
    Status.Text=english?"Removing the panel… The plugin card may close.":"Удаление панели… Карточка плагина может закрыться.";
    try{var result=await Task.Run(()=>{using(var lease=QuotaPanelTaskHost.AcquireUninstallLock())return PanelUninstall.Run(plan,remove,operations());});Status.Text=result.Complete?(english?(remove?"Plugin and settings removed.":"Plugin removed. Settings were kept."):(remove?"Плагин и настройки удалены.":"Плагин удалён. Настройки сохранены.")):(english?"Plugin removed. These folders could not be cleaned:\n":"Плагин удалён. Не удалось очистить папки:\n")+String.Join("\n",result.Remaining);}
    catch{Status.Text=english?"Removal could not be completed. The helper did not confirm full removal.":"Удаление не удалось завершить. Полное удаление не подтверждено.";}
    finally{running=false;finished=true;CancelRemoval.Enabled=true;CancelRemoval.Text=english?"Close":"Закрыть";}
  }
}

internal static class PanelUninstallerProgram {
  [STAThread]static void Main(string[] args){
    bool created;string sid=WindowsIdentity.GetCurrent().User.Value;
    using(var singleton=new Mutex(true,"Local\\ChatGPTLimitPanel.Uninstall."+sid,out created)){
      try{
        if(!created)return;
        if(args.Length!=6||args[0]!="--root"||args[2]!="--home"||args[4]!="--language"||!new[]{"ru","en"}.Contains(args[5]))throw new IOException("Invalid uninstall request");
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var plan=PanelUninstall.CreatePlan(args[1],args[3],local);
        string codex=LimitPanelLauncher.DiscoverCodex(local,"auto",Path.GetDirectoryName(plan.Launcher));
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        using(var window=new UninstallWindow(plan,args[5],()=>new PanelUninstall.NativeOperations(codex)))Application.Run(window);
      }catch{MessageBox.Show(args.Contains("en")?"Could not prepare plugin removal. The installation was not changed.":"Не удалось подготовить удаление плагина. Установка не изменена.","ChatGPT Limit Panel",MessageBoxButtons.OK,MessageBoxIcon.Error);}
      finally{CleanupTemporaryCopy();}
    }
  }
  static void CleanupTemporaryCopy(){try{
    string directory=PanelUninstall.Full(AppDomain.CurrentDomain.BaseDirectory),temporary=PanelUninstall.Full(Path.GetTempPath());
    if(!String.Equals(Path.GetDirectoryName(directory),temporary,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(directory).StartsWith("ChatGPTLimitPanel-uninstall-",StringComparison.Ordinal))return;
    string file=Path.Combine(directory,"ChatGPTLimitPanelUninstaller.exe");if(Directory.GetFileSystemEntries(directory).Length!=1||!File.Exists(file))return;
    // Remove only this helper file and its now-empty temporary directory.
    string script="Wait-Process -Id "+Process.GetCurrentProcess().Id+" -ErrorAction SilentlyContinue; Remove-Item -LiteralPath '"+file.Replace("'","''")+"' -ErrorAction SilentlyContinue; Remove-Item -LiteralPath '"+directory.Replace("'","''")+"' -ErrorAction SilentlyContinue";
    Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script))){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=temporary});
  }catch{}}
}
