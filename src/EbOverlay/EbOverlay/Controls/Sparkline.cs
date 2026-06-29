using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Pen   = System.Windows.Media.Pen;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace EbOverlay.Controls;

/// <summary>
/// Scrolling sparkline graph for unbounded metrics like network traffic.
/// Call Push() with each new sample — the line redraws automatically.
/// Peak value in the current buffer is used as the Y ceiling.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    private double[] _values = [];
    private double   _peak   = 1.0; // avoid divide-by-zero on empty buffer

    private static readonly Pen  LinePen  = new(new SolidColorBrush(Color.FromArgb(0xAA, 0x66, 0xFF, 0x99)), 1.0);
    private static readonly Brush FillBrush = new SolidColorBrush(Color.FromArgb(0x22, 0x66, 0xFF, 0x99));

    public void Update(double[] values, double peak)
    {
        _values = values;
        _peak   = peak > 0 ? peak : 1.0;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_values.Length < 2) return;

        double w    = ActualWidth;
        double h    = ActualHeight;
        double step = w / (_values.Length - 1);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double x0 = 0;
            double y0 = h - (_values[0] / _peak * h);
            ctx.BeginFigure(new Point(x0, y0), isFilled: true, isClosed: false);

            for (int i = 1; i < _values.Length; i++)
            {
                double x = i * step;
                double y = h - (_values[i] / _peak * h);
                ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: true);
            }

            // Close fill down to baseline
            ctx.LineTo(new Point(w, h), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(0, h), isStroked: false, isSmoothJoin: false);
        }

        geometry.Freeze();
        dc.DrawGeometry(FillBrush, LinePen, geometry);
    }
}
