using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

internal static class LayeredChildManifestTest {
  static int checks;
  static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
  static int Main(){IntPtr parent=IntPtr.Zero,child=IntPtr.Zero;try{
    parent=CreateWindowEx(0x08000080,"STATIC",null,0x80000000,-25000,-25000,160,80,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
    Check(parent!=IntPtr.Zero,"create only an own hidden native parent");
    child=CreateWindowEx(0x08080080,"STATIC",null,0x40000000,0,0,72,48,parent,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
    int error=Marshal.GetLastWin32Error();
    Check(child!=IntPtr.Zero,"embedded application manifest must enable layered child creation; native error "+error);
    Check(GetParent(child)==parent,"layered child belongs only to the own parent");
    Check(SetLayeredWindowAttributes(child,0,255,2),"native child layering is available without changing another process");
    Check(AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(child),new IntPtr(-4)),"modern compatibility preserves PerMonitorV2");
    IntPtr resource=FindResource(IntPtr.Zero,new IntPtr(1),new IntPtr(24));Check(resource!=IntPtr.Zero,"compiled executable includes its own application manifest");
    uint size=SizeofResource(IntPtr.Zero,resource);IntPtr loaded=LoadResource(IntPtr.Zero,resource),data=LockResource(loaded);
    Check(size>0&&size<65536&&data!=IntPtr.Zero,"read only bounded own executable manifest resource");
    byte[] bytes=new byte[size];Marshal.Copy(data,bytes,0,bytes.Length);var doc=new XmlDocument();doc.LoadXml(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').TrimEnd('\0'));
    var ns=new XmlNamespaceManager(doc.NameTable);ns.AddNamespace("a","urn:schemas-microsoft-com:asm.v1");ns.AddNamespace("c","urn:schemas-microsoft-com:compatibility.v1");ns.AddNamespace("v","urn:schemas-microsoft-com:asm.v3");ns.AddNamespace("d","http://schemas.microsoft.com/SMI/2016/WindowsSettings");
    Check(doc.SelectNodes("/a:assembly/c:compatibility/c:application/c:supportedOS[@Id='{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}']",ns).Count==1,"compiled manifest explicitly targets Windows10/11 child composition");
    Check(doc.SelectSingleNode("/a:assembly/v:application/v:windowsSettings/d:dpiAwareness",ns).InnerText=="PerMonitorV2","compiled manifest retains original DPI behavior");
    var privilege=(XmlElement)doc.SelectSingleNode("/a:assembly/v:trustInfo/v:security/v:requestedPrivileges/v:requestedExecutionLevel",ns);
    Check(privilege!=null&&privilege.GetAttribute("level")=="asInvoker"&&privilege.GetAttribute("uiAccess")=="false","compatibility adds no privilege or input-access request");
    Console.WriteLine("PASS native child compatibility manifest ("+checks+" assertions)");return 0;
  }catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}finally{if(child!=IntPtr.Zero)DestroyWindow(child);if(parent!=IntPtr.Zero)DestroyWindow(parent);}}
  [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateWindowEx(uint extended,string name,string text,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr args);
  [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr window);
  [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr window);
  [DllImport("user32.dll")]static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
  [DllImport("user32.dll")]static extern bool AreDpiAwarenessContextsEqual(IntPtr left,IntPtr right);
  [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
  [DllImport("kernel32.dll",EntryPoint="FindResourceW")]static extern IntPtr FindResource(IntPtr module,IntPtr name,IntPtr type);
  [DllImport("kernel32.dll")]static extern uint SizeofResource(IntPtr module,IntPtr resource);
  [DllImport("kernel32.dll")]static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
  [DllImport("kernel32.dll")]static extern IntPtr LockResource(IntPtr resource);
}
