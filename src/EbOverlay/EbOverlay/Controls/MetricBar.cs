using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace EbOverlay.Controls;

/// <summary>
/// Two-tone horizontal bar. LeftRatio fills from the left in accent color,
/// RightRatio continues in a dimmer color. Used for app|system CPU and RAM.
/// Both ratios are 0.0–1.0.
/// </summary>
public sealed class MetricBar : FrameworkElement
{
    public static readonly DependencyProperty LeftRatioProperty =
        DependencyProperty.Register(nameof(LeftRatio), typeof(double), typeof(MetricBar),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RightRatioProperty =
        DependencyProperty.Register(nameof(RightRatio), typeof(double), typeof(MetricBar),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double LeftRatio
    {
        get => (double)GetValue(LeftRatioProperty);
        set => SetValue(LeftRatioProperty, Math.Clamp(value, 0, 1));
    }

    public double RightRatio
    {
        get => (double)GetValue(RightRatioProperty);
        set => SetValue(RightRatioProperty, Math.Clamp(value, 0, 1));
    }

    private static readonly Brush TrackBrush  = new SolidColorBrush(Color.FromArgb(0x33, 0x66, 0xFF, 0x99));
    private static readonly Brush LeftBrush   = new SolidColorBrush(Color.FromArgb(0xCC, 0x66, 0xFF, 0x99));
    private static readonly Brush RightBrush  = new SolidColorBrush(Color.FromArgb(0x66, 0x66, 0xFF, 0x99));
    private static readonly Brush HighBrush   = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0x66, 0x66));

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;

        // Track (empty background)
        dc.DrawRectangle(TrackBrush, null, new Rect(0, 0, w, h));

        double rightPx = RightRatio * w;
        double leftPx  = LeftRatio  * w;

        // System-wide fill (dimmer, behind)
        if (rightPx > 0)
            dc.DrawRectangle(RightBrush, null, new Rect(0, 0, rightPx, h));

        // App fill (brighter, on top) — red when high
        if (leftPx > 0)
        {
            var brush = RightRatio >= 0.7 ? HighBrush : LeftBrush;
            dc.DrawRectangle(brush, null, new Rect(0, 0, leftPx, h));
        }
    }
}
