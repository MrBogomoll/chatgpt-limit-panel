using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class TaskbarPassiveOverlayTest {
  static int assertions;
  delegate bool Metadata(string name,long style,long extended,bool unowned,bool sameForegroundProcess,bool trustedImage);
  delegate bool TrustedPath(string image,string programFiles);
  delegate bool NativeCanvas(IntPtr window);
  delegate bool Above(IntPtr candidate,IntPtr reference);
  static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
  static T Method<T>(Type policy,string name) where T:class{return Delegate.CreateDelegate(typeof(T),policy.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)) as T;}
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
  [STAThread]static int Main(){
    try {
      Type policy=typeof(TaskbarPassiveOverlayTest).Assembly.GetType("QuotaOverlay.TaskbarPassiveOverlay");
      Check(policy!=null,"passive overlay policy must classify the observed disabled NVIDIA canvas before occupancy can choose a protected underlay");
      Metadata metadata=Method<Metadata>(policy,"IsPassiveCanvasMetadata");TrustedPath trusted=Method<TrustedPath>(policy,"IsTrustedImagePath");
      NativeCanvas canvas=Method<NativeCanvas>(policy,"IsPassiveCanvas");Above above=Method<Above>(policy,"IsAbove");
      const long actualStyle=0x9C000000L,actualExtended=0x000800A8L;
      Check(metadata("CEF-OSC-WIDGET",actualStyle,actualExtended,true,false,true),"the measured disabled, click-through NVIDIA canvas is eligible only for placement below it; flags0 alpha is not evidence of invisibility");
      Check(metadata("CEF-OSC-WIDGET",unchecked((int)0x9C000000),actualExtended,true,false,true),"signed native styles retain the same guarded passive classification");
      Check(metadata("CEF-OSC-WIDGET",actualStyle,actualExtended|0x08000000L,true,false,true),"no-activate does not make an otherwise exact passive canvas interactive");
      foreach(string name in new[]{"CEF-OSC-WIDGET ","cef-osc-widget","CEF-OSC-WIDGET2","Chrome_WidgetWin_1","NetWorxDeskband","",null})Check(!metadata(name,actualStyle,actualExtended,true,false,true),"a lookalike class cannot gain the protected-underlay classification: "+name);
      Check(!metadata("CEF-OSC-WIDGET",actualStyle,actualExtended,false,false,true),"owned NVIDIA dialogs remain protected UI");
      Check(!metadata("CEF-OSC-WIDGET",actualStyle,actualExtended,true,true,true),"foreground NVIDIA UI cannot be classified as a passive background canvas");
      Check(!metadata("CEF-OSC-WIDGET",actualStyle,actualExtended,true,false,false),"a trusted-looking class and styles cannot replace executable provenance");
      foreach(long bit in new long[]{0x80000000L,0x10000000L,0x08000000L})Check(!metadata("CEF-OSC-WIDGET",actualStyle&~bit,actualExtended,true,false,true),"popup, visible and disabled styles are each required: "+bit.ToString("X"));
      foreach(long bit in new long[]{0x40000000L,0x00800000L,0x00400000L,0x00040000L})Check(!metadata("CEF-OSC-WIDGET",actualStyle|bit,actualExtended,true,false,true),"child, caption/frame and resizable shapes remain real UI: "+bit.ToString("X"));
      foreach(long bit in new long[]{0x00080000L,0x20L,0x80L,8L})Check(!metadata("CEF-OSC-WIDGET",actualStyle,actualExtended&~bit,true,false,true),"layered, click-through, tool-window and topmost flags are each required: "+bit.ToString("X"));
      Check(!metadata("CEF-OSC-WIDGET",actualStyle,actualExtended|0x00040000L,true,false,true),"an app-window cannot be treated as a passive canvas");
      string root=@"C:\Program Files",image=@"C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe";
      Check(trusted(image,root),"only the exact protected NVIDIA App CEF executable establishes provenance");
      Check(trusted(image.ToUpperInvariant(),root.ToLowerInvariant()),"Windows image identity is case insensitive");
      Check(trusted(@"D:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe",@"D:\Program Files"),"the actual Program Files directory may be on another local drive");
      foreach(string path in new[]{@"C:\Temp\NVIDIA Overlay.exe",@"C:\Users\user\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe",@"C:\Program Files\NVIDIA Corporation\NVIDIA AppFake\CEF\NVIDIA Overlay.exe",@"C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe.bak",@"C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe:stream",@"C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\..\NVIDIA Overlay.exe",@"C:\Program Files (x86)\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe",@"NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe",@"C:NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe",@"\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe",@"\\server\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe","",null,"\0"})Check(!trusted(path,root),"lookalike, writable, relative or malformed paths cannot grant a canvas exception: "+path);
      foreach(string invalidRoot in new[]{"",null,"\0",@"Program Files",@"C:Program Files",@"\Program Files",@"\\server\Program Files"})Check(!trusted(image,invalidRoot),"a missing or non-local Program Files root cannot establish protected provenance");
      Check(!canvas(IntPtr.Zero),"no native window is never a passive canvas");
      Check(!canvas(new IntPtr(0x123)),"an invalid native handle fails closed");
      Check(!above(IntPtr.Zero,new IntPtr(0x123)),"unknown candidate cannot establish an underlay order");
      Check(!above(new IntPtr(0x123),IntPtr.Zero),"unknown reference cannot establish an underlay order");
      IntPtr foreground=GetForegroundWindow();
      using(var own=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Left=-20000,Top=-20000,Width=32,Height=32}) {
        IntPtr handle=own.Handle;
        Check(!above(handle,handle),"a window is not strictly above itself");
        for(int i=0;i<5;i++)Check(!canvas(handle),"the current application's ordinary window cannot gain NVIDIA provenance");
        IntPtr previous=GetWindow(handle,3);
        if(previous!=IntPtr.Zero){Check(above(previous,handle),"the actual adjacent native predecessor is above the own fixture");Check(!above(handle,previous),"native Z order must not be reversed");}
        Check(GetForegroundWindow()==foreground,"read-only policy checks and the unshown fixture preserve foreground");
      }
      Console.WriteLine("PASS passive overlay policy ("+assertions+" assertions)");return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}
  }
}
