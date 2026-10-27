using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;

namespace QuotaOverlay {
  internal static class TaskbarAppearance {
    static readonly Brush inputSurface=CreateInputSurface();
    static readonly ControlTemplate sliderTemplate=CreateSliderTemplate();

    // A layered HWND with alpha-zero background passes native mouse input
    // through its empty pixels. This invisible input surface keeps the entire
    // panel usable without changing the visual panel brush or its text opacity.
    internal static Brush InputSurface(int transparency) {return transparency==100?inputSurface:Brushes.Transparent;}
    static Brush CreateInputSurface() {var brush=new SolidColorBrush(Color.FromArgb(1,0,0,0));brush.Freeze();return brush;}

    internal static void ApplySliderStyle(Slider slider) {
      slider.Template=sliderTemplate;
      slider.Height=24;slider.MinHeight=24;slider.Padding=new Thickness(0);
      slider.BorderThickness=new Thickness(0);slider.Background=Brushes.Transparent;
      slider.FocusVisualStyle=null;slider.Focusable=false;
      slider.SnapsToDevicePixels=true;slider.UseLayoutRounding=true;
    }
    static ControlTemplate CreateSliderTemplate() {
      const string source=@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type Slider}'>
  <Grid Height='24' Background='Transparent' SnapsToDevicePixels='True'>
    <Border Height='4' CornerRadius='2' VerticalAlignment='Center'
            Background='{DynamicResource Quota.MenuDivider}'/>
    <Track x:Name='PART_Track' IsDirectionReversed='{TemplateBinding IsDirectionReversed}'
           Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}'
           Value='{TemplateBinding Value}'>
      <Track.DecreaseRepeatButton>
        <RepeatButton Command='{x:Static Slider.DecreaseLarge}' Focusable='False' IsTabStop='False'>
          <RepeatButton.Template>
            <ControlTemplate TargetType='{x:Type RepeatButton}'>
              <Grid Background='Transparent'>
                <Border Height='4' CornerRadius='2' VerticalAlignment='Center'
                        Background='{DynamicResource Quota.MenuCheck}'/>
              </Grid>
            </ControlTemplate>
          </RepeatButton.Template>
        </RepeatButton>
      </Track.DecreaseRepeatButton>
      <Track.IncreaseRepeatButton>
        <RepeatButton Command='{x:Static Slider.IncreaseLarge}' Focusable='False' IsTabStop='False'>
          <RepeatButton.Template>
            <ControlTemplate TargetType='{x:Type RepeatButton}'>
              <Grid Background='Transparent'>
                <Border Height='4' CornerRadius='2' VerticalAlignment='Center'
                        Background='{DynamicResource Quota.MenuDivider}'/>
              </Grid>
            </ControlTemplate>
          </RepeatButton.Template>
        </RepeatButton>
      </Track.IncreaseRepeatButton>
      <Track.Thumb>
        <Thumb Width='22' Height='22' Focusable='False'>
          <Thumb.Template>
            <ControlTemplate TargetType='{x:Type Thumb}'>
              <Grid Background='Transparent'>
                <Ellipse x:Name='Halo' Width='22' Height='22' Opacity='0'
                         Fill='{DynamicResource Quota.MenuCheck}'/>
                <Ellipse Width='14' Height='14' StrokeThickness='2'
                         Stroke='{DynamicResource Quota.MenuSurface}'
                         Fill='{DynamicResource Quota.MenuCheck}'/>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Halo' Property='Opacity' Value='.14'/></Trigger>
                <Trigger Property='IsDragging' Value='True'><Setter TargetName='Halo' Property='Opacity' Value='.24'/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
          </Thumb.Template>
        </Thumb>
      </Track.Thumb>
    </Track>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";
      using(var text=new StringReader(source))using(var xml=XmlReader.Create(text))return (ControlTemplate)XamlReader.Load(xml);
    }
  }
}
