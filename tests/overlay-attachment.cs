using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuotaOverlay;

internal static class AttachmentNativeTest {
  delegate IntPtr Procedure(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
  static readonly Procedure callback=WindowProcedure;
  static int assertions;
  static readonly List<Window> windows=new List<Window>();
  static readonly List<Process> processes=new List<Process>();
  [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct WindowClass{public uint Size,Style;public Procedure Procedure;public int ClassExtra,WindowExtra;public IntPtr Instance,Icon,Cursor,Background;public string Menu,Name;public IntPtr SmallIcon;}
  [StructLayout(LayoutKind.Sequential)]struct Message{public IntPtr Hwnd;public uint Id;public IntPtr WParam,LParam;public uint Time;public int X,Y;public uint Private;}
  sealed class Host {internal Process Process;internal IntPtr Handle;}
  static void Check(bool good,string message){assertions++;if(!good)throw new Exception(message);}
  static void Pump(int milliseconds=40){var clock=Stopwatch.StartNew();do{var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);Thread.Sleep(5);}while(clock.ElapsedMilliseconds<milliseconds);}
  static IntPtr WindowProcedure(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam){if(message==2){PostQuitMessage(0);return IntPtr.Zero;}return DefWindowProc(hwnd,message,wParam,lParam);}
  static int Parent(string file,bool mismatch){var cls=new WindowClass{Size=(uint)Marshal.SizeOf(typeof(WindowClass)),Procedure=callback,Name="QuotaOwnAttachmentBoundary",Instance=GetModuleHandle(null)};if(RegisterClassEx(ref cls)==0)return 2;IntPtr old=mismatch?SetThreadDpiAwarenessContext(new IntPtr(-2)):IntPtr.Zero;IntPtr hwnd=CreateWindowEx(0x08080080,cls.Name,null,0x80000000,-22000,-22000,480,72,IntPtr.Zero,IntPtr.Zero,cls.Instance,IntPtr.Zero);if(mismatch)SetThreadDpiAwarenessContext(old);if(hwnd==IntPtr.Zero)return 3;SetLayeredWindowAttributes(hwnd,0,0,2);ShowWindow(hwnd,4);File.WriteAllText(file,new JavaScriptSerializer().Serialize(new {handle=hwnd.ToInt64(),pid=Process.GetCurrentProcess().Id}));Message message;while(GetMessage(out message,IntPtr.Zero,0,0)>0){TranslateMessage(ref message);DispatchMessage(ref message);}return 0;}
  static Host StartHost(string root,bool mismatch=false){string file=Path.Combine(root,Guid.NewGuid().ToString("N")+".json");var process=Process.Start(new ProcessStartInfo{FileName=Process.GetCurrentProcess().MainModule.FileName,Arguments="parent \""+file+"\" "+(mismatch?"system":"pmv2"),UseShellExecute=false,CreateNoWindow=true});processes.Add(process);var watch=Stopwatch.StartNew();Dictionary<string,object> data=null;while(data==null&&!process.HasExited&&watch.ElapsedMilliseconds<3000){try{if(File.Exists(file))data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(file));}catch(IOException){}catch(ArgumentException){}if(data==null)Thread.Sleep(10);}Check(data!=null,"Separate own fake parent starts");return new Host{Process=process,Handle=new IntPtr(Convert.ToInt64(data["handle"]))};}
  static Window OwnWindow(){var window=new Window{Left=-24000,Top=-24000,Width=48,Height=32,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,ShowActivated=false,ShowInTaskbar=false,Focusable=false,Opacity=0,Content=new System.Windows.Controls.Border{Background=Brushes.Gray,Child=new System.Windows.Controls.TextBlock{Text="73%\n42%",Foreground=Brushes.White}}};windows.Add(window);window.Show();Pump();SetWindowLongPtr(new WindowInteropHelper(window).Handle,-8,IntPtr.Zero);HwndSource.FromHwnd(new WindowInteropHelper(window).Handle).AddHook(delegate(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});return window;}
  static TaskbarSnapshot Snapshot(IntPtr parent){Rect rectangle;GetWindowRect(parent,out rectangle);return new TaskbarSnapshot{Handle=parent,Bounds=new TaskbarArea{X=rectangle.Left,Y=rectangle.Top,Width=rectangle.Right-rectangle.Left,Height=rectangle.Bottom-rectangle.Top},Scale=GetDpiForWindow(parent)/96.0,Visible=true,OccupancyKnown=true};}
  static TaskbarArea Placement(TaskbarSnapshot sample){return new TaskbarArea{X=sample.Bounds.X+24,Y=sample.Bounds.Y+12,Width=72,Height=48};}
  static void Fixture(string root){
    IntPtr foreground=GetForegroundWindow();var host=StartHost(root);var window=OwnWindow();IntPtr own=new WindowInteropHelper(window).Handle;string reason;
    var strict=new TaskbarAttachment(delegate{return host.Handle;});Check(!strict.TryAttach(own,out reason),"Default shell identity rejects a fake parent without touching real Explorer");Check(GetParent(own)!=host.Handle,"Rejected fake identity does not reparent own HWND");Check(GetParent(own)==IntPtr.Zero,"Ownerless own fixture exercises successful SetParent zero return with last-error zero");
    var boundary=new TaskbarAttachment(delegate{return host.Handle;},delegate(IntPtr h){return h==host.Handle;});
    Check(!boundary.TryAttach(host.Handle,out reason),"A foreign-process HWND cannot be passed as the own indicator");
    Check(boundary.TryAttach(own,out reason),"Owned PMv2 WPF HWND attaches to separately owned matching fake host: "+reason);
    Check(boundary.Parent==host.Handle&&boundary.IsAlive&&boundary.IsAttached,"Attachment publishes verified parent and live child relation");
    Check(window.Opacity==0,"Attachment never restores pixels before renderer validation");
    Check(GetParent(own)==host.Handle&&GetAncestor(own,2)==host.Handle,"Own child and fake host share native root");
    Check((GetWindowLongPtr(own,-16).ToInt64()&0xC0000000L)==0x40000000L,"Own HWND has WS_CHILD and no WS_POPUP");Check((GetWindowLongPtr(own,-20).ToInt64()&8)==0,"Child has no independent topmost band");
    Check(AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(own),GetWindowDpiAwarenessContext(host.Handle)),"Cross-process attachment preserves exact matching DPI contexts");
    var sample=Snapshot(host.Handle);var placement=Placement(sample);Check(boundary.TryPlace(own,sample,placement,out reason),"Verified physical allocation maps into fake parent client: "+reason);Pump();Rect rect;GetWindowRect(own,out rect);Check(rect.Left==placement.X&&rect.Top==placement.Y&&rect.Right-rect.Left==72&&rect.Bottom-rect.Top==48,"Native physical child rectangle exactly matches allocation");
    var replacement=StartHost(root);IntPtr originalHost=host.Handle;host.Handle=replacement.Handle;Check(!boundary.IsAlive&&!boundary.TryAttach(own,out reason),"A different live parent generation cannot silently adopt the existing child");Check(GetParent(own)==originalHost,"Host replacement rejection leaves current native parent unchanged");host.Handle=originalHost;Check(boundary.IsAlive,"Restoring the same still-live resolver identity retains original attachment");
    long style=GetWindowLongPtr(own,-16).ToInt64(),extended=GetWindowLongPtr(own,-20).ToInt64();IntPtr parent=GetParent(own);var wrong=Snapshot(host.Handle);wrong.Bounds.X+=1;Check(!boundary.TryPlace(own,wrong,placement,out reason),"Stale parent geometry is rejected");Check(GetParent(own)==parent&&GetWindowLongPtr(own,-16).ToInt64()==style&&GetWindowLongPtr(own,-20).ToInt64()==extended,"A stale snapshot does not mutate attachment styles or parent");
    wrong=Snapshot(host.Handle);wrong.Scale=Double.NaN;Check(!boundary.TryPlace(own,wrong,placement,out reason),"Invalid persisted scale is rejected");var outside=placement;outside.X=sample.Bounds.X+sample.Bounds.Width;Check(!boundary.TryPlace(own,sample,outside,out reason),"Outside-client allocation is rejected");
    var fraction=placement;fraction.X+=0.5;Check(!boundary.TryPlace(own,sample,fraction,out reason),"Noninteger physical allocation is rejected rather than rounded");
    Check(SetWindowPos(host.Handle,IntPtr.Zero,-21000,-20000,480,72,0x0014),"Only own fake host moves");Pump();GetWindowRect(own,out rect);Check(rect.Left==-21000+24&&rect.Top==-20000+12,"Child follows native parent without a tracker");
    ShowWindow(host.Handle,0);Pump();Check(boundary.IsAlive&&boundary.IsAttached&&!IsWindowVisible(own),"Host hide keeps a live attachment but inherits native hidden state");sample=Snapshot(host.Handle);Check(!boundary.TryPlace(own,sample,Placement(sample),out reason),"Placement on a hidden host is rejected without detaching");
    ShowWindow(host.Handle,4);Pump();sample=Snapshot(host.Handle);Check(boundary.TryPlace(own,sample,Placement(sample),out reason)&&IsWindowVisible(own),"Host restore recovers on same attachment");
    ShowWindow(host.Handle,6);Pump();Check(IsIconic(host.Handle),"Own fake host reaches iconic state");sample=Snapshot(host.Handle);Check(!boundary.TryPlace(own,sample,Placement(sample),out reason),"Iconic host is rejected even if child WS_VISIBLE remains set");ShowWindow(host.Handle,4);Pump();sample=Snapshot(host.Handle);Check(boundary.TryPlace(own,sample,Placement(sample),out reason),"Restored host accepts fresh geometry");
    window.Opacity=1;window.UpdateLayout();var bitmap=new RenderTargetBitmap(72,48,144,144,PixelFormats.Pbgra32);bitmap.Render(window);byte[] pixels=new byte[72*48*4];bitmap.CopyPixels(pixels,72*4,0);int filled=0;for(int i=3;i<pixels.Length;i+=4)if(pixels[i]>0)filled++;Check(filled>72,"Attached transparent WPF visual still renders content");Check(window.InputHitTest(new Point(10,10))!=null,"Child visual retains input hit testing");GetWindowRect(own,out rect);long packed=((long)(short)(rect.Top+10)<<16)|(ushort)(rect.Left+10);Check(SendMessage(own,0x84,IntPtr.Zero,new IntPtr(packed)).ToInt64()==1,"Child native hit test is HTCLIENT");Check(SendMessage(own,0x21,host.Handle,IntPtr.Zero).ToInt64()==3,"Mouse activation remains MA_NOACTIVATE");
    Check(GetForegroundWindow()==foreground,"All attachment and fake-host transitions preserve foreground");
    PostMessage(host.Handle,0x10,IntPtr.Zero,IntPtr.Zero);var clock=Stopwatch.StartNew();while(IsWindow(own)&&clock.ElapsedMilliseconds<2000)Pump(10);Check(!IsWindow(own)&&!boundary.IsAlive&&!boundary.IsAttached,"Host destruction invalidates own native child and attachment");Check(!boundary.TryAttach(own,out reason),"Destroyed child cannot be silently reused");
    var mismatch=StartHost(root,true);var fresh=OwnWindow();IntPtr freshHwnd=new WindowInteropHelper(fresh).Handle;long beforeStyle=GetWindowLongPtr(freshHwnd,-16).ToInt64();IntPtr beforeParent=GetParent(freshHwnd),beforeContext=GetWindowDpiAwarenessContext(freshHwnd);var incompatible=new TaskbarAttachment(delegate{return mismatch.Handle;},delegate{return true;});Check(!incompatible.TryAttach(freshHwnd,out reason),"DPI mismatch rejects cross-process attachment");Check(GetParent(freshHwnd)==beforeParent&&GetWindowLongPtr(freshHwnd,-16).ToInt64()==beforeStyle&&AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(freshHwnd),beforeContext),"Mismatch rejects before own style/parent/DPI mutation");
    var valid=StartHost(root);Check(SetWindowPos(freshHwnd,new IntPtr(-1),0,0,0,0,0x0013),"Own failure fixture begins in an independent topmost band");int failedCalls=0;bool failParent=true;var failed=new TaskbarAttachment(delegate{return valid.Handle;},delegate{return true;},delegate(IntPtr child,IntPtr target){failedCalls++;return SetParent(failParent?IntPtr.Zero:child,target);});Check(!failed.TryAttach(freshHwnd,out reason),"Actual native SetParent error is returned as failure");Check(failedCalls==1&&reason=="set-parent-failed","Injected parent failure must be reached and classified (calls="+failedCalls+",reason="+reason+")");Check(beforeParent==IntPtr.Zero,"Native failure fixture has no hidden WPF owner");Check(GetParent(freshHwnd)==beforeParent&&GetWindowLongPtr(freshHwnd,-16).ToInt64()==beforeStyle,"Failed native parent call restores the exact original unowned top-level style");Check((GetWindowLongPtr(freshHwnd,-20).ToInt64()&8)==0,"A native attachment failure never restores an independent topmost fallback");failParent=false;Check(failed.TryAttach(freshHwnd,out reason)&&failed.IsAttached,"The same helper can retry after native failure without a poisoned parent or child style");
    Check(GetForegroundWindow()==foreground,"Mismatch and native-failure paths preserve foreground");
  }
  [STAThread]static int Main(string[] args){if(args.Length==3&&args[0]=="parent")return Parent(args[1],args[2]=="system");try{Fixture(args[0]);Console.WriteLine("PASS native attachment ("+assertions+" assertions)");return 0;}catch(Exception error){Console.Error.WriteLine(error.GetType().Name+": "+error.Message);return 1;}finally{foreach(var window in windows)try{window.Close();}catch{}foreach(var process in processes){try{if(!process.HasExited){PostMessage(new IntPtr(FindOwnParent(process.Id)),0x10,IntPtr.Zero,IntPtr.Zero);if(!process.WaitForExit(1000))process.Kill();}}catch{}process.Dispose();}}}
  static long FindOwnParent(int pid){long found=0;EnumWindows(delegate(IntPtr h,IntPtr data){uint process;GetWindowThreadProcessId(h,out process);if(process==(uint)pid){found=h.ToInt64();return false;}return true;},IntPtr.Zero);return found;}
  delegate bool EnumCallback(IntPtr hwnd,IntPtr data);
  [DllImport("user32.dll")]static extern bool EnumWindows(EnumCallback callback,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern ushort RegisterClassEx(ref WindowClass value);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateWindowEx(uint extended,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr data);
  [DllImport("user32.dll")]static extern IntPtr DefWindowProc(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll")]static extern int GetMessage(out Message message,IntPtr hwnd,uint min,uint max);
  [DllImport("user32.dll")]static extern bool TranslateMessage(ref Message message);
  [DllImport("user32.dll")]static extern IntPtr DispatchMessage(ref Message message);
  [DllImport("user32.dll")]static extern void PostQuitMessage(int code);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
  [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetParent(IntPtr child,IntPtr parent);
  [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr hwnd);
  [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
  [DllImport("user32.dll")]static extern bool IsWindow(IntPtr hwnd);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll")]static extern bool IsIconic(IntPtr hwnd);
  [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr hwnd,int command);
  [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr hwnd,uint key,byte alpha,uint flags);
  [DllImport("user32.dll")]static extern bool PostMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
  [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
  [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr hwnd);
  [DllImport("user32.dll")]static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
  [DllImport("user32.dll")]static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
  [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
  [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
}
