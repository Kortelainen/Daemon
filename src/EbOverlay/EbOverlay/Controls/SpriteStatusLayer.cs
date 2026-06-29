using System.Windows;
using System.Windows.Media;
using EbOverlay.Zones;
using Color = System.Windows.Media.Color;
using Pen   = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace EbOverlay.Controls;

/// <summary>
/// Transparent overlay drawn on top of the sprite Image.
/// Renders small status icons in stacked corner slots — top-right by default.
/// Slots fill top-to-bottom so the most urgent icon (first in the set) sits highest.
///
/// To add a new icon: add a case to the switch in DrawIcon and implement a Draw* method.
/// </summary>
public sealed class SpriteStatusLayer : FrameworkElement
{
    private static readonly IReadOnlySet<StatusIcon> Empty = new HashSet<StatusIcon>();

    private IReadOnlyList<StatusIcon> _ordered = [];

    // Slot geometry — each icon occupies a 16×20 slot stacked from the top-right corner
    private const double SlotW    = 16;
    private const double SlotH    = 20;
    private const double SlotPadX = 2;   // from right edge
    private const double SlotPadY = 2;   // from top edge

    public void SetIcons(IEnumerable<StatusIcon> icons)
    {
        _ordered = [.. icons];
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        for (int i = 0; i < _ordered.Count; i++)
        {
            double x = ActualWidth  - SlotW - SlotPadX;
            double y = SlotPadY + i * SlotH;
            DrawIcon(dc, _ordered[i], x, y);
        }
    }

    // ── Icon dispatch ─────────────────────────────────────────────────────────

    private static void DrawIcon(DrawingContext dc, StatusIcon icon, double x, double y) =>
        _ = icon switch
        {
            StatusIcon.SweatDrop => DrawSweatDrop(dc, x, y),
            StatusIcon.Flame     => DrawFlame(dc, x, y),
            StatusIcon.Lightning => DrawLightning(dc, x, y),
            StatusIcon.AppBolt   => DrawAppBolt(dc, x, y),
            _                    => false,
        };

    // ── Sweat drop ────────────────────────────────────────────────────────────
    // Classic anime sweat bead — teardrop pointing up, light blue

    private static bool DrawSweatDrop(DrawingContext dc, double x, double y)
    {
        double cx = x + SlotW / 2;
        double tipY  = y + 1;
        double baseY = y + SlotH - 3;
        double r = (baseY - tipY) * 0.42;

        var body = new StreamGeometry();
        using (var ctx = body.Open())
        {
            ctx.BeginFigure(new Point(cx, tipY), true, true);
            ctx.BezierTo(
                new Point(cx - r * 1.1, tipY + (baseY - tipY) * 0.4),
                new Point(cx - r,       baseY - r),
                new Point(cx,           baseY), true, true);
            ctx.BezierTo(
                new Point(cx + r,       baseY - r),
                new Point(cx + r * 1.1, tipY + (baseY - tipY) * 0.4),
                new Point(cx,           tipY), true, true);
        }
        body.Freeze();

        dc.DrawGeometry(
            new SolidColorBrush(Color.FromArgb(210, 100, 190, 255)),
            new Pen(new SolidColorBrush(Color.FromArgb(160, 60, 130, 210)), 0.8),
            body);

        // Shine highlight — small white ellipse near top-left of drop
        dc.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
            null, new Point(cx - r * 0.3, tipY + r * 0.6), r * 0.28, r * 0.28);

        return true;
    }

    // ── Flame ─────────────────────────────────────────────────────────────────
    // Critical heat — orange/red flame shape

    private static bool DrawFlame(DrawingContext dc, double x, double y)
    {
        double cx   = x + SlotW / 2;
        double topY = y + 1;
        double botY = y + SlotH - 2;
        double h    = botY - topY;

        // Outer flame
        var outer = new StreamGeometry();
        using (var ctx = outer.Open())
        {
            ctx.BeginFigure(new Point(cx, topY), true, true);
            ctx.BezierTo(new Point(cx + 5, topY + h * 0.25),
                         new Point(cx + 6, botY - 3),
                         new Point(cx,     botY), true, true);
            ctx.BezierTo(new Point(cx - 6, botY - 3),
                         new Point(cx - 5, topY + h * 0.25),
                         new Point(cx,     topY), true, true);
        }
        outer.Freeze();

        // Inner bright core
        var inner = new StreamGeometry();
        using (var ctx = inner.Open())
        {
            ctx.BeginFigure(new Point(cx, topY + h * 0.35), true, true);
            ctx.BezierTo(new Point(cx + 3, topY + h * 0.55),
                         new Point(cx + 3, botY - 4),
                         new Point(cx,     botY - 2), true, true);
            ctx.BezierTo(new Point(cx - 3, botY - 4),
                         new Point(cx - 3, topY + h * 0.55),
                         new Point(cx,     topY + h * 0.35), true, true);
        }
        inner.Freeze();

        dc.DrawGeometry(
            new SolidColorBrush(Color.FromArgb(220, 240, 100, 20)),
            null, outer);
        dc.DrawGeometry(
            new SolidColorBrush(Color.FromArgb(200, 255, 220, 80)),
            null, inner);

        return true;
    }

    // ── Lightning bolt ────────────────────────────────────────────────────────
    // High system CPU — classic zigzag bolt

    private static bool DrawLightning(DrawingContext dc, double x, double y)
    {
        double cx   = x + SlotW / 2;
        double topY = y + 1;
        double botY = y + SlotH - 2;

        // Points for a simple bolt: top-right → mid-left → mid-right → bottom-left
        var pts = new Point[]
        {
            new(cx + 3, topY),
            new(cx - 1, topY + (botY - topY) * 0.48),
            new(cx + 2, topY + (botY - topY) * 0.48),
            new(cx - 3, botY),
        };

        var bolt = new StreamGeometry();
        using (var ctx = bolt.Open())
        {
            ctx.BeginFigure(pts[0], true, true);
            ctx.LineTo(pts[1], true, true);
            ctx.LineTo(pts[2], true, true);
            ctx.LineTo(pts[3], true, true);
            // close back up the other side with slight width
            ctx.LineTo(new Point(pts[3].X + 2.5, botY - 1),         true, true);
            ctx.LineTo(new Point(pts[2].X + 2,   pts[2].Y),         true, true);
            ctx.LineTo(new Point(pts[1].X + 2.5, pts[1].Y),         true, true);
            ctx.LineTo(new Point(pts[0].X + 0.5, topY + 1.5),       true, true);
        }
        bolt.Freeze();

        dc.DrawGeometry(
            new SolidColorBrush(Color.FromArgb(230, 255, 230, 60)),
            new Pen(new SolidColorBrush(Color.FromArgb(160, 200, 160, 0)), 0.5),
            bolt);

        return true;
    }

    // ── App bolt ──────────────────────────────────────────────────────────────
    // High foreground-app CPU — like Lightning but smaller and teal-tinted

    private static bool DrawAppBolt(DrawingContext dc, double x, double y)
    {
        // Nudge inward slightly so it looks like a secondary indicator
        DrawLightning(dc, x + 1, y + 2);

        // Teal tint overlay to distinguish from the pure-yellow system bolt
        double cx   = x + SlotW / 2 + 1;
        double midY = y + SlotH / 2 + 1;
        dc.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(40, 60, 230, 220)),
            null, new Point(cx, midY), 5, 7);

        return true;
    }
}
