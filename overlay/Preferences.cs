using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace QuotaOverlay {
  internal static class Preferences {
    sealed class Lease:IDisposable {
      readonly Mutex mutex;bool owned;
      public Lease(string path) {
        byte[] hash;using(var algorithm=SHA256.Create())hash=algorithm.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()));
        mutex=new Mutex(false,"Local\\QuotaPanel.Settings."+BitConverter.ToString(hash).Replace("-","").Substring(0,24));
        try{owned=mutex.WaitOne(5000);}catch(AbandonedMutexException){owned=true;}
        if(!owned){mutex.Dispose();throw new IOException("Settings are busy");}
      }
      public void Dispose(){if(owned)mutex.ReleaseMutex();mutex.Dispose();}
    }
    static Dictionary<string,object> ReadUnlocked(string path) {
      try{return new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path)) as Dictionary<string,object>??new Dictionary<string,object>();}catch{return new Dictionary<string,object>();}
    }
    internal static Dictionary<string,object> Read(string path) {using(var lease=new Lease(path))return ReadUnlocked(path);}
    internal static void Update(string path,Action<Dictionary<string,object>> update) {
      using(var lease=new Lease(path)){var values=ReadUnlocked(path);update(values);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));File.WriteAllText(path,new JavaScriptSerializer().Serialize(values),Encoding.UTF8);}
    }
    internal static void ConfigureAutostart(string path,bool? requested,Action<bool> configureTask) {
      using(var control=new Lease(path+".autostart-control")) {
        var values=Read(path);object saved;
        bool enabled=requested??(!values.TryGetValue("autoStartEnabled",out saved)||!(saved is bool)||(bool)saved);
        configureTask(enabled);
        Update(path,data=>{data["autoStartEnabled"]=enabled;data["autostartSetupVersion"]=2;});
      }
    }
  }
}
