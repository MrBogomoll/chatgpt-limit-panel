using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using QuotaOverlay;

internal static class AttachedRendererTest {
  static int assertions;
  [StructLayout(LayoutKind.Sequential)]struct Rect {internal int Left,Top,Right,Bottom;}
  static void Check(bool value,string text){assertions++;if(!value)throw new Exception(text);}
  static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);}
  static void Refresh(TaskbarWindow window){typeof(TaskbarWindow).GetMethod("RefreshPlacement",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,null);Pump();}
  static void SourceStallRetention() {
    IntPtr host=IntPtr.Zero;TaskbarWindow window=null;TaskbarSnapshotSource source=null;
    var blocked=new ManualResetEvent(false);var release=new ManualResetEvent(false);long now=0;int scans=0;string fingerprint="native-controls-A";
    try {
      host=CreateWindowEx(0x08080080,"STATIC",null,0x80000000,-25500,-25500,960,72,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
      Check(host!=IntPtr.Zero&&SetLayeredWindowAttributes(host,0,0,2),"create only own transparent source-stall host");ShowWindow(host,4);
      Rect rect;Check(GetWindowRect(host,out rect),"measure own source-stall host");
      var sample=new TaskbarSnapshot{Handle=host,Bounds=new TaskbarArea{X=rect.Left,Y=rect.Top,Width=rect.Right-rect.Left,Height=rect.Bottom-rect.Top},Scale=GetDpiForWindow(host)/96.0,Visible=true,OccupancyKnown=true};
      source=new TaskbarSnapshotSource(delegate{if(Interlocked.Increment(ref scans)==2){blocked.Set();release.WaitOne();}return sample;},delegate{return Interlocked.Read(ref now);},delegate{return true;},true);
      var attachment=new TaskbarAttachment(delegate{return host;},delegate(IntPtr value){return value==host;});
      window=new TaskbarWindow(null,delegate{return true;},delegate{return fingerprint;},delegate{return Interlocked.Read(ref now);},null,attachment,source);
      window.Configure(true,"right",ThemePalette.Dark,100);
      ((DispatcherTimer)typeof(TaskbarWindow).GetField("timer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window)).Stop();
      var wait=Stopwatch.StartNew();while(!window.IsVisible&&wait.ElapsedMilliseconds<1000){Refresh(window);Thread.Sleep(2);}
      Check(window.IsVisible&&window.PlacementStatus=="attached","initial real source acquisition displays the verified child");
      Interlocked.Exchange(ref now,350);Refresh(window);Check(blocked.WaitOne(1000),"next real source acquisition is deliberately stalled");
      Interlocked.Exchange(ref now,1601);Refresh(window);
      Check(window.IsVisible&&window.PlacementStatus=="layout-held","continuous current native proof retains the displayed child when the real source cache expires during a stall");
      Interlocked.Exchange(ref now,5000);Refresh(window);Check(window.IsVisible,"active native retention survives an ongoing provider stall without renewing UIA acquisition time");
      fingerprint="native-controls-B";Interlocked.Exchange(ref now,5200);Refresh(window);Check(!window.IsVisible,"an actual native control-evidence change revokes stalled-source retention");
      Interlocked.Exchange(ref now,5300);Refresh(window);Check(!window.IsVisible,"unknown allocation cannot revive a child after native evidence changed");
      release.Set();Interlocked.Exchange(ref now,6000);
      wait.Restart();while(!window.IsVisible&&wait.ElapsedMilliseconds<1000){Refresh(window);Thread.Sleep(2);}
      Check(window.IsVisible&&window.PlacementStatus=="attached"&&scans>=3,"a newly measured complete source frame recovers without a normal desktop or transient close");
    }finally{release.Set();if(window!=null)window.Dispose();else if(source!=null)source.Dispose();if(host!=IntPtr.Zero)DestroyWindow(host);}
  }
  [STAThread]static int Main(){IntPtr host=IntPtr.Zero;TaskbarWindow window=null;try{
    IntPtr foreground=GetForegroundWindow();
    host=CreateWindowEx(0x08080080,"STATIC",null,0x80000000,-26000,-26000,960,72,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
    Check(host!=IntPtr.Zero&&SetLayeredWindowAttributes(host,0,0,2),"create only an invisible own native host");
    ShowWindow(host,4);
    Rect bar;Check(GetWindowRect(host,out bar),"read own host geometry");
    var sample=new TaskbarSnapshot{Handle=host,Bounds=new TaskbarArea{X=bar.Left,Y=bar.Top,Width=bar.Right-bar.Left,Height=bar.Bottom-bar.Top},Scale=GetDpiForWindow(host)/96.0,Visible=true,OccupancyKnown=true};
    TaskbarSnapshot current=sample;bool transient=true;
    var attachment=new TaskbarAttachment(delegate{return host;},delegate(IntPtr value){return value==host;});
    window=new TaskbarWindow(delegate{return current;},delegate{return transient;},delegate{return "verified-native-controls";},null,null,attachment);
    window.ContextMenu=new ContextMenu{Visibility=Visibility.Collapsed};window.ContextMenu.Items.Add(new MenuItem{Header="Own fixture"});
    window.ApplyRows("73%","42%","normal","normal",false);
    window.Configure(true,"right",ThemePalette.Dark,100);
    ((DispatcherTimer)typeof(TaskbarWindow).GetField("timer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window)).Stop();Pump();
    IntPtr own=window.NativeHandle;
    Check(window.IsVisible&&GetParent(own)==host,"fresh complete evidence can first show a native child while Start-like transient remains active");
    Check((GetWindowLongPtr(own,-16).ToInt64()&0x40000000L)!=0&&(GetWindowLongPtr(own,-20).ToInt64()&8)==0,"renderer uses a child without independent topmost");
    Check(new WindowInteropHelper(window).Handle==IntPtr.Zero,"native path creates no top-level WPF Window to reparent");
    var nativeSource=HwndSource.FromHwnd(own);var root=(Grid)nativeSource.RootVisual;
    Check(nativeSource.UsesPerPixelOpacity,"source created as a child uses the modern per-pixel composition path");
    Check(root.ContextMenu==window.ContextMenu,"actual child visual owns the configured context menu");
    Check(((SolidColorBrush)root.Background).Color.A==1,"completely clear child keeps a full-area native input surface");
    Check(root.InputHitTest(new Point(5,5))!=null,"clear background remains WPF hit-testable");
    Check((GetWindowLongPtr(own,-20).ToInt64()&0x20)==0,"native child never uses input-transparent extended style");
    Check(SendMessage(own,0x21,host,IntPtr.Zero).ToInt64()==3,"child refuses mouse activation");
    Rect hit;Check(GetWindowRect(own,out hit),"read own native input rectangle");long packed=((long)(short)(hit.Top+5)<<16)|(ushort)(hit.Left+5);
    Check(SendMessage(own,0x84,IntPtr.Zero,new IntPtr(packed)).ToInt64()==1,"clear native background is HTCLIENT at full transparency");
    SendMessage(own,0x7B,own,new IntPtr(packed));Pump();
    Check(window.ContextMenu.IsOpen,"native context-menu message reaches the child visual");window.ContextMenu.IsOpen=false;Pump();
    for(int cycle=0;cycle<3;cycle++){
      sample.Occupied.Add(sample.Bounds);Refresh(window);
      Check(!window.IsVisible,"an actual control contradiction conceals the indicator");
      current=null;Refresh(window);Check(!window.IsVisible,"unknown allocation cannot revive a hidden child");
      sample.Occupied.Clear();current=sample;Refresh(window);
      Check(window.IsVisible&&GetParent(own)==host,"fresh safe allocation recovers without a normal desktop or transient close");
    }
    current=null;Refresh(window);Check(window.IsVisible&&window.PlacementStatus=="layout-held","a visible child retains only its verified footprint during acquisition failure");
    current=sample;Refresh(window);
    Rect before,after;Check(GetWindowRect(own,out before),"read attached child position");
    Check(SetWindowPos(host,IntPtr.Zero,bar.Left+40,bar.Top+30,0,0,0x0015),"move only own test parent");
    Check(GetWindowRect(own,out after)&&after.Left-before.Left==40&&after.Top-before.Top==30,"parent movement moves the child without a placement tick");
    ShowWindow(host,0);Check(!IsWindowVisible(own),"parent hiding clips the native child without input");
    ShowWindow(host,4);
    Check(GetWindowRect(host,out bar),"read moved host geometry");sample.Bounds=new TaskbarArea{X=bar.Left,Y=bar.Top,Width=bar.Right-bar.Left,Height=bar.Bottom-bar.Top};Refresh(window);
    Check(window.IsVisible&&GetParent(own)==host,"fresh parent geometry remains attached after host show");
    window.Configure(false,"right",ThemePalette.Dark,100);Check(!window.IsVisible,"explicit disable hides the child");
    window.Configure(true,"right",ThemePalette.Dark,100);((DispatcherTimer)typeof(TaskbarWindow).GetField("timer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window)).Stop();Pump();
    Check(window.IsVisible,"explicit enable preserves native attachment");
    Check(DestroyWindow(host),"destroy only the own host");host=IntPtr.Zero;Pump();
    Check(window.NeedsRecreation,"destroyed native parent requires a new WPF child instance");
    Check(GetForegroundWindow()==foreground,"all attachment, recovery and parent operations preserve user foreground");
    SourceStallRetention();Check(GetForegroundWindow()==foreground,"real source stall and rearming preserve user foreground");
    Console.WriteLine("PASS attached renderer ("+assertions+" assertions)");return 0;
  }catch(Exception error){Console.Error.WriteLine(error.GetType().Name+": "+error.Message);return 1;}finally{if(window!=null)window.Dispose();if(host!=IntPtr.Zero)DestroyWindow(host);}}
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateWindowEx(uint ex,string name,string text,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr parameter);
  [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr window,uint color,byte alpha,uint flags);
  [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr window);
  [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
  [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect rect);
  [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr window);
  [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr window);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
}
