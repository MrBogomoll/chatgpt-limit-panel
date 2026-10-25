using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuotaOverlay {
  // Scale from width only. A transient old viewport height must never distort
  // the badge or its text while the native window changes presentation mode.
  public sealed class QuotaVisualHost : Decorator {
    public QuotaVisualHost() {ClipToBounds=true;}
    protected override Size MeasureOverride(Size available) {
      if(Child==null)return new Size();
      Child.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));
      double scale=Double.IsInfinity(available.Width)||Child.DesiredSize.Width==0?1:available.Width/Child.DesiredSize.Width;
      double height=Child.DesiredSize.Height*scale;
      return new Size(Child.DesiredSize.Width*scale,Double.IsInfinity(available.Height)?height:Math.Min(available.Height,height));
    }
    protected override Size ArrangeOverride(Size viewport) {
      if(Child!=null) {
        // Keep one size negotiation. A nested Viewbox can retain the previous
        // desired height after the HWND and the preview update independently.
        Child.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));
        Size natural=Child.DesiredSize;
        double scale=natural.Width==0?1:viewport.Width/natural.Width;
        // Native width/height are rounded independently to physical pixels.
        // Fit that rounding difference without treating an old mode's viewport
        // as a request to shrink the entire badge during a real transition.
        var source=PresentationSource.FromVisual(this);
        double pixelsPerDip=source==null?1:source.CompositionTarget.TransformToDevice.M22;
        double roundingSlack=natural.Width==0?0:.5*(1+natural.Height/natural.Width)/pixelsPerDip;
        double excess=natural.Height*scale-viewport.Height;
        if(natural.Height>0 && excess>0 && excess<=roundingSlack+1e-6)scale=viewport.Height/natural.Height;
        Child.RenderTransform=new ScaleTransform(scale,scale);
        Child.Arrange(new Rect((viewport.Width-natural.Width*scale)/2,viewport.Height-natural.Height*scale,natural.Width,natural.Height));
      }
      return viewport;
    }
  }
}
