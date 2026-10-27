using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using QuotaOverlay;

class TaskbarAppearanceTest {
  static int assertions;
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect bounds);
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr parameter,IntPtr value);
  static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
  static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject {
    if(root is T)yield return (T)root;
    for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants<T>(VisualTreeHelper.GetChild(root,i)))yield return child;
  }
  static Color ColorOf(Brush brush){return ((SolidColorBrush)brush).Color;}
  static void Palette(FrameworkElement root,ThemePalette palette) {
    root.Resources["Quota.MenuSurface"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette.MenuSurface));
    root.Resources["Quota.MenuDivider"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette.MenuDivider));
    root.Resources["Quota.MenuCheck"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette.MenuCheck));
  }
  static void Layout(FrameworkElement root,Size size){root.Measure(size);root.Arrange(new Rect(new Point(),size));root.UpdateLayout();}
  static RenderTargetBitmap Render(FrameworkElement root,int width,int height,double dpi=96) {
    var bitmap=new RenderTargetBitmap(width,height,dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(root);return bitmap;
  }
  static void Save(RenderTargetBitmap image,string path){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path))encoder.Save(file);}
  [STAThread] static int Main(string[] args) {
    try {
      IntPtr foreground=GetForegroundWindow();
      Check(ColorOf(TaskbarAppearance.InputSurface(0)).A==0,"opaque visual panels need no additional native input surface");
      Check(ColorOf(TaskbarAppearance.InputSurface(50)).A==0,"ordinary transparency must keep the exact requested background alpha");
      Check(ColorOf(TaskbarAppearance.InputSurface(100)).A==1,"the fully clear visual background must retain a nonzero native mouse surface");
      var input=new Border{Width=120,Height=60,Background=TaskbarAppearance.InputSurface(100)};
      Layout(input,new Size(120,60));var pixel=new byte[4];Render(input,120,60).CopyPixels(new Int32Rect(60,30,1,1),pixel,4,0);
      Check(pixel[3]==1,"an empty center pixel must remain native-hit-testable at maximum transparency");
      var visualSurface=new Border{Width=120,Height=60};
      var host=new Window{Left=-22000,Top=-22000,Width=120,Height=60,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=TaskbarAppearance.InputSurface(100),ShowActivated=false,ShowInTaskbar=false,Focusable=false,Content=visualSurface};
      try{
        host.Show();host.UpdateLayout();IntPtr handle=new WindowInteropHelper(host).Handle;
        foreach(int transparency in new[]{0,50,100}) {
          host.Background=TaskbarAppearance.InputSurface(transparency);
          visualSurface.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(ThemePalette.Dark.Background(transparency)));
          host.UpdateLayout();
          Check(host.InputHitTest(new Point(60,30))!=null,"the entire empty panel must participate in WPF input at "+transparency+"%");
          var actualPixel=new byte[4];Render(host,120,60).CopyPixels(new Int32Rect(60,30,1,1),actualPixel,4,0);
          Check(actualPixel[3]>0,"the actual layered window must have a nonzero native input pixel at "+transparency+"%");
          if(transparency==100)Check(actualPixel[3]==1,"maximum transparency must use only the visually negligible input pixel");
          NativeRect bounds;Check(GetWindowRect(handle,out bounds),"own native panel bounds must be available");
          int x=(bounds.Left+bounds.Right)/2,y=(bounds.Top+bounds.Bottom)/2;
          IntPtr point=new IntPtr(unchecked((int)(((uint)y&0xFFFF)<<16|((uint)x&0xFFFF))));
          Check(SendMessage(handle,0x0084,IntPtr.Zero,point).ToInt64()==1,"native hit testing must identify client input at "+transparency+"%");
          long flags=GetWindowLongPtr(handle,-20).ToInt64();
          Check((flags&0x80000)!=0&&(flags&0x20)==0,"the input layer must remain layered without native click-through");
        }
      }finally{host.Close();}
      Check(ThemePalette.Dark.Background(100).StartsWith("#00")&&ThemePalette.Light.Background(100).StartsWith("#00"),"input retention must not change the visual surface alpha contract");

      var slider=new Slider{Minimum=0,Maximum=100,Value=29,Width=195,IsSnapToTickEnabled=true,TickFrequency=1,IsMoveToPointEnabled=true};
      TaskbarAppearance.ApplySliderStyle(slider);
      var label=new TextBlock{Text="Прозрачность фона: 29%",FontFamily=new FontFamily("Segoe UI"),FontSize=12,Margin=new Thickness(0,0,0,6)};
      var stack=new StackPanel{Margin=new Thickness(14)};stack.Children.Add(label);stack.Children.Add(slider);
      var card=new Border{Width=225,Height=90,CornerRadius=new CornerRadius(9),Child=stack};
      Palette(card,ThemePalette.Dark);card.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(ThemePalette.Dark.MenuSurface));label.Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString(ThemePalette.Dark.MenuText));
      Layout(card,new Size(225,90));slider.ApplyTemplate();
      var track=(Track)slider.Template.FindName("PART_Track",slider);Check(track!=null,"modern template must retain WPF's named slider track");
      Check(track.Thumb!=null&&track.Thumb.Width==22&&track.Thumb.Height==22,"small visual thumb must keep a comfortable pointer target");
      Check(Descendants<Ellipse>(track.Thumb).Any(e=>e.Width==14&&e.Height==14),"the thumb must render as a compact circular handle");
      Check(slider.ActualHeight==24&&!slider.Focusable,"modern styling must preserve nonactivating pointer interaction");
      Check(slider.Minimum==0&&slider.Maximum==100&&slider.Value==29&&slider.TickFrequency==1&&slider.IsMoveToPointEnabled,"styling must preserve the existing range, value and click behavior");
      int changed=0;slider.ValueChanged+=delegate{changed++;};slider.Value=0;slider.Value=100;slider.Value=29;
      Check(changed==3,"the production value-change path must still fire at both endpoints");
      Check(Math.Abs(track.Value-29)<0.01,"the custom track must remain synchronized with the slider value");
      var accent=Descendants<Border>(track.DecreaseRepeatButton).First(b=>b.Height==4);
      Check(ColorOf(accent.Background)==(Color)ColorConverter.ConvertFromString(ThemePalette.Dark.MenuCheck),"active track must use the current dark-theme accent");
      string directory=args.Length>0?args[0]:System.IO.Path.GetTempPath();Directory.CreateDirectory(directory);
      Save(Render(card,338,135,144),System.IO.Path.Combine(directory,"slider-dark.png"));
      Palette(card,ThemePalette.Light);card.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(ThemePalette.Light.MenuSurface));label.Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString(ThemePalette.Light.MenuText));Layout(card,new Size(225,90));
      Check(ColorOf(accent.Background)==(Color)ColorConverter.ConvertFromString(ThemePalette.Light.MenuCheck),"an existing template must follow live light-theme resource changes");
      Check(ColorOf(label.Foreground).A==255&&slider.Opacity==1,"modern slider styling must not fade readable labels or controls");
      Save(Render(card,338,135,144),System.IO.Path.Combine(directory,"slider-light.png"));
      Check(GetForegroundWindow()==foreground,"own component rendering must preserve the active application");
      Console.WriteLine("PASS modern slider and transparent input surface ("+assertions+" assertions)");return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}
  }
}
