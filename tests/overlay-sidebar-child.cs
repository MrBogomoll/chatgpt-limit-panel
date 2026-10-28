using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuotaOverlay;
class SidebarChildTest {
  static int assertions;
  [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
  static void Check(bool value,string text){assertions++;if(!value)throw new Exception(text);}
  static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{f.Continue=false;}));Dispatcher.PushFrame(f);}
  static IntPtr Host(){var h=CreateWindowEx(0x08080080,"STATIC",null,0x80000000,-26000,-26000,960,900,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);Check(h!=IntPtr.Zero&&SetLayeredWindowAttributes(h,0,0,2),"create transparent own host");ShowWindow(h,4);return h;}
  static void Verify(Grid wrapper,QuotaVisualHost visual,TaskbarChildSurface child,IntPtr parent,TaskbarAttachment attach){
    string reason;Rect r;Check(GetWindowRect(parent,out r),"measure own parent");double scale=GetDpiForWindow(parent)/96.0;
    var snapshot=new TaskbarSnapshot{Handle=parent,Visible=true,Scale=scale,Bounds=new TaskbarArea{X=r.Left,Y=r.Top,Width=r.Right-r.Left,Height=r.Bottom-r.Top}};
    var slot=new TaskbarArea{X=r.Left+3,Y=r.Top+300,Width=Math.Round(46*scale),Height=Math.Round(100*scale)};
    Check(attach.TryPlace(child.Handle,snapshot,slot,out reason),"place own child:"+reason);child.Show();child.Opacity=1;Pump();wrapper.UpdateLayout();Pump();
    Check(child.IsAlive&&child.IsVisible&&GetParent(child.Handle)==parent&&attach.IsAttached,"child retains native attachment");
    Check(PresentationSource.FromVisual(visual)==HwndSource.FromHwnd(child.Handle),"same visual attached to source");
    Check(((SolidColorBrush)wrapper.Background).Color.A==1,"100 percent transparency preserves alpha1 host background");
    Check(wrapper.InputHitTest(new Point(5,80))!=null,"blank panel remains hit testable");
    var bitmap=new RenderTargetBitmap((int)slot.Width,(int)slot.Height,96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(wrapper);
    var pixels=new byte[(int)slot.Width*(int)slot.Height*4];bitmap.CopyPixels(pixels,(int)slot.Width*4,0);int alpha=pixels[((int)(80*scale)*(int)slot.Width+(int)(5*scale))*4+3];
    Check(alpha==1,"blank pixel retains exact alpha1, actual="+alpha);bool glyph=false;for(int i=3;i<pixels.Length;i+=4)if(pixels[i]>100){glyph=true;break;}Check(glyph,"same visual renders quota glyph after migration");
    Check(SendMessage(child.Handle,0x21,parent,IntPtr.Zero).ToInt64()==3,"mouse activation rejected");
    long point=((long)(short)(slot.Y+5)<<16)|(ushort)(slot.X+5);Check(SendMessage(child.Handle,0x84,IntPtr.Zero,new IntPtr(point)).ToInt64()==1,"native hit test accepts panel clicks");
  }
  [STAThread]static int Main(){Window owner=null;TaskbarChildSurface child=null;TaskbarAttachment attach=null;IntPtr host=IntPtr.Zero;try{
    owner=new Window{ShowActivated=false,ShowInTaskbar=false,Width=46,Height=100};
    var text=new TextBlock{Text="73%",Foreground=Brushes.White,FontSize=14,VerticalAlignment=VerticalAlignment.Top};
    var visual=new QuotaVisualHost{Child=new Border{Width=46,Height=100,Background=Brushes.Transparent,Child=text}};
    owner.Content=visual;new WindowInteropHelper(owner).EnsureHandle();owner.Content=null;
    host=Host();var wrapper=new Grid{Background=TaskbarAppearance.InputSurface(100)};wrapper.Children.Add(visual);child=new TaskbarChildSurface(wrapper,"Own isolated sidebar test");
    string reason;bool lost=false;IntPtr verifiedParent;attach=new TaskbarAttachment(delegate{return host;},delegate(IntPtr h){return h==host;});Check(attach.TryGetCreationParent(out verifiedParent,out reason),"verify first parent:"+reason);
    HwndSourceHook hook=delegate(IntPtr h,int m,IntPtr w,IntPtr l,ref bool handled){if(m==0x84){handled=true;return new IntPtr(1);}return IntPtr.Zero;};
    Check(child.TryCreate(verifiedParent,hook,delegate{lost=true;},out reason),"create first source:"+reason);Verify(wrapper,visual,child,host,attach);
    Check(DestroyWindow(host),"destroy only first own parent");host=IntPtr.Zero;Pump();Check(!child.IsAlive,"parent destroy invalidates child");Console.WriteLine("closedCallbackAfterParentDestroy="+lost);
    child.Dispose();child=null;attach.Dispose();attach=null;wrapper.Children.Remove(visual);wrapper=null;owner.Content=visual;
    Check(owner.Content==visual&&visual.Opacity==1,"restore same visible visual to hidden Window");owner.Content=null;
    host=Host();wrapper=new Grid{Background=TaskbarAppearance.InputSurface(100)};wrapper.Children.Add(visual);child=new TaskbarChildSurface(wrapper,"Own isolated sidebar test recreated");
    attach=new TaskbarAttachment(delegate{return host;},delegate(IntPtr h){return h==host;});Check(attach.TryGetCreationParent(out verifiedParent,out reason),"verify second parent:"+reason);Check(child.TryCreate(verifiedParent,hook,delegate{},out reason),"recreate source:"+reason);Verify(wrapper,visual,child,host,attach);
    child.Opacity=0;child.Dispose();child=null;attach.Dispose();attach=null;wrapper.Children.Remove(visual);owner.Content=visual;Check(visual.Opacity==1,"wrapper opacity0 does not poison fallback original visual");
    Console.WriteLine("PASS isolated sidebar lifetime/render/input ("+assertions+" assertions)");return 0;
  }catch(Exception e){Console.WriteLine(e.ToString());return 1;}finally{if(child!=null)child.Dispose();if(attach!=null)attach.Dispose();if(host!=IntPtr.Zero)DestroyWindow(host);if(owner!=null)owner.Close();}}
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateWindowEx(uint ex,string cls,string text,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
  [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr window);
  [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
  [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect rect);
  [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr window,uint color,byte alpha,uint flags);
  [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr window);
  [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr window);
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
}