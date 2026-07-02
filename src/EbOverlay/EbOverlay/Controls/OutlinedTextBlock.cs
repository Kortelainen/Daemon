using System.Windows;
using System.Windows.Media;
using Brush      = System.Windows.Media.Brush;
using Brushes    = System.Windows.Media.Brushes;
using Color      = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Pen        = System.Windows.Media.Pen;
using Point      = System.Windows.Point;
using Size       = System.Windows.Size;

namespace Daemon.Controls;

/// <summary>
/// Text rendered as geometry — drawn twice to produce a solid outline on all sides.
/// Outline pass uses a thick black Pen; fill pass draws the text color on top.
/// Properties mirror the most-used TextBlock properties so XAML styles translate directly.
/// </summary>
public sealed class OutlinedTextBlock : FrameworkElement
{
    // ── Dependency properties ────────────────────────────────────────────────

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(new FontFamily("Courier New"), FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(13.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontWeightProperty =
        DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(FontWeights.Bold, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OutlineThicknessProperty =
        DependencyProperty.Register(nameof(OutlineThickness), typeof(double), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextAlignmentProperty =
        DependencyProperty.Register(nameof(TextAlignment), typeof(TextAlignment), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(TextAlignment.Left, FrameworkPropertyMetadataOptions.AffectsRender));

    // ── CLR wrappers ─────────────────────────────────────────────────────────

    public string        Text             { get => (string)GetValue(TextProperty);             set => SetValue(TextProperty, value); }
    public FontFamily    FontFamily       { get => (FontFamily)GetValue(FontFamilyProperty);   set => SetValue(FontFamilyProperty, value); }
    public double        FontSize         { get => (double)GetValue(FontSizeProperty);         set => SetValue(FontSizeProperty, value); }
    public FontWeight    FontWeight       { get => (FontWeight)GetValue(FontWeightProperty);   set => SetValue(FontWeightProperty, value); }
    public Brush         Foreground       { get => (Brush)GetValue(ForegroundProperty);        set => SetValue(ForegroundProperty, value); }
    public double        OutlineThickness { get => (double)GetValue(OutlineThicknessProperty); set => SetValue(OutlineThicknessProperty, value); }
    public TextAlignment TextAlignment    { get => (TextAlignment)GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

    // ── Shared outline pen (black, semi-transparent for softness) ─────────────
    private Pen BuildOutlinePen() =>
        new(new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)), OutlineThickness * 2.0)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap   = PenLineCap.Round,
        };

    // ── Measure ───────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        var ft = BuildFormattedText(availableSize.Width);
        // Add outline thickness to each side so the outline never clips
        double pad = OutlineThickness;
        return new Size(ft.Width + pad * 2, ft.Height + pad * 2);
    }

    // ── Render ────────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;

        double pad = OutlineThickness;
        var origin = new Point(pad, pad);

        var ft       = BuildFormattedText(ActualWidth - pad * 2);
        var geometry = ft.BuildGeometry(origin);

        // Pass 1 — outline (thick pen, no fill)
        dc.DrawGeometry(null, BuildOutlinePen(), geometry);

        // Pass 2 — fill (no pen, foreground color)
        dc.DrawGeometry(Foreground, null, geometry);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private FormattedText BuildFormattedText(double maxWidth)
    {
        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal);

        var ft = new FormattedText(
            Text ?? string.Empty,
            System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Foreground,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            TextAlignment = TextAlignment,
            MaxTextWidth  = double.IsFinite(maxWidth) && maxWidth > 1 ? maxWidth : 4096,
        };

        return ft;
    }

}
