using System;
using QuotaOverlay;
class PreferencesTest {
  static int Main(string[] args){try{
    if(args[1]=="data-read"||args[1]=="data-migrate"){Console.OutputEncoding=new System.Text.UTF8Encoding(false);Console.Write(Preferences.DataDirectory(args[1]=="data-migrate",args[0]));return 0;}
    if(args[1]=="control-start"||args[1]=="control-disable") {
      bool start=args[1]=="control-start";
      Preferences.ConfigureAutostart(args[0],start?(bool?)null:false,enabled=>{
        if(start){System.IO.File.WriteAllText(args[0]+".read","read");System.Threading.Thread.Sleep(500);}
        System.IO.File.WriteAllText(args[0]+".task",enabled?"true":"false");
      });return 0;
    }
    for(int i=0;i<30;i++){int value=i;Preferences.Update(args[0],data=>{if(args[1]=="theme")data["themeMode"]=value%2==0?"dark":"light";else data["autoStartEnabled"]=value%2==0;});}
    return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
