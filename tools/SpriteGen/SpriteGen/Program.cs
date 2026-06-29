using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;

// ── Sprite sheet spec ────────────────────────────────────────────────────────
const int FrameW    = 96;
const int FrameH    = 96;
const int MaxFrames = 8;    // sheet width in frames — all rows padded to this

var states = new (string Name, int Frames, Color Tint)[]
{
    ("Sleep",       8, Color.FromArgb(180, 140, 200)),  // lavender
    ("Idle",        8, Color.FromArgb(140, 200, 160)),  // mint
    ("IdleCurious", 6, Color.FromArgb(140, 190, 200)),  // sky
    ("WakeUp",      6, Color.FromArgb(160, 200, 220)),  // light blue
    ("WinSwitch",   5, Color.FromArgb(200, 180, 140)),  // sand
    ("WinOpen",     5, Color.FromArgb(160, 220, 160)),  // green
    ("WinClose",    5, Color.FromArgb(200, 160, 160)),  // rose
    ("Smile",       6, Color.FromArgb(220, 210, 140)),  // yellow
    ("Stare",       4, Color.FromArgb(160, 140, 200)),  // purple
    ("CpuHigh",     8, Color.FromArgb(220, 160, 120)),  // orange
    ("CpuHighApp",  6, Color.FromArgb(220, 140, 100)),  // deep orange
    ("HeatWarn",    8, Color.FromArgb(230, 150, 80)),   // amber
    ("HeatCritical",8, Color.FromArgb(240, 80,  60)),   // red
};

int sheetW = MaxFrames * FrameW;
int sheetH = states.Length * FrameH;

using var sheet = new Bitmap(sheetW, sheetH, PixelFormat.Format32bppArgb);
using var g     = Graphics.FromImage(sheet);
g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
g.Clear(Color.Transparent);

using var labelFont  = new Font("Courier New", 7f,  FontStyle.Bold);
using var stateFont  = new Font("Courier New", 6.5f, FontStyle.Regular);
using var dimBrush   = new SolidBrush(Color.FromArgb(120, 255, 255, 255));
using var borderPen  = new Pen(Color.FromArgb(50, 255, 255, 255), 1f);
using var emptyPen   = new Pen(Color.FromArgb(30, 255, 255, 255), 1f);

for (int row = 0; row < states.Length; row++)
{
    var (name, frameCount, tint) = states[row];

    for (int frame = 0; frame < MaxFrames; frame++)
    {
        int px = frame * FrameW;
        int py = row   * FrameH;
        var rect = new Rectangle(px, py, FrameW, FrameH);

        bool active = frame < frameCount;

        // ── Background ───────────────────────────────────────────────────────
        int bgAlpha = active ? 30 : 12;
        using (var bgBrush = new SolidBrush(Color.FromArgb(bgAlpha, tint)))
            g.FillRectangle(bgBrush, rect);

        g.DrawRectangle(active ? borderPen : emptyPen, rect);

        if (!active)
        {
            // Empty slot — thin diagonal cross so it reads as intentionally blank
            g.DrawLine(emptyPen, px + 4, py + 4, px + FrameW - 4, py + FrameH - 4);
            g.DrawLine(emptyPen, px + FrameW - 4, py + 4, px + 4, py + FrameH - 4);
            continue;
        }

        // ── Turnip character ─────────────────────────────────────────────────
        DrawTurnip(g, px, py, frame, row, tint, name);

        // ── Frame label (bottom-left) ─────────────────────────────────────────
        using var lb = new SolidBrush(Color.FromArgb(180, tint));
        g.DrawString($"R{row}F{frame}", labelFont, lb, px + 2, py + FrameH - 13);

        // ── State name on first frame only ────────────────────────────────────
        if (frame == 0)
        {
            using var nb = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
            g.DrawString(name, stateFont, nb, px + 2, py + 2);
        }
    }
}

string outPath = @"C:\Kortelainen\EbOverlay\src\EbOverlay\EbOverlay\Sprites\spritesheet.png";
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
sheet.Save(outPath, ImageFormat.Png);
Console.WriteLine($"Saved → {outPath}");
Console.WriteLine($"Sheet size: {sheetW}×{sheetH}px  ({MaxFrames} cols × {states.Length} rows × {FrameW}×{FrameH}px frames)");

// ── Drawing helpers ──────────────────────────────────────────────────────────

static void DrawTurnip(Graphics g, int px, int py, int frame, int row, Color tint, string state)
{
    // Animate: slight vertical bob on even frames, blink on frame 3 (or 7)
    int  bob    = (frame % 2 == 0) ? 0 : 1;
    bool blink  = frame == 3 || frame == 7;
    bool squint = state.Contains("Heat") || state == "CpuHigh" || state == "CpuHighApp";
    bool smile  = state == "Smile" || state == "Idle" || state == "WakeUp";
    bool sleep  = state == "Sleep";
    bool stare  = state == "Stare";
    bool worried = state is "HeatWarn" or "HeatCritical" or "CpuHigh";

    // Body (rounded rectangle, turnip-shaped)
    using var bodyBrush = new SolidBrush(Color.FromArgb(210, Lerp(tint, Color.White, 0.3f)));
    g.FillEllipse(bodyBrush, px + 30, py + 46 + bob, 36, 34);

    // Head
    using var headBrush = new SolidBrush(Color.FromArgb(230, Lerp(tint, Color.White, 0.5f)));
    g.FillEllipse(headBrush, px + 24, py + 18 + bob, 48, 44);

    // Cheek blush
    using var blushBrush = new SolidBrush(Color.FromArgb(60, 255, 120, 120));
    g.FillEllipse(blushBrush, px + 27, py + 36 + bob, 10, 7);
    g.FillEllipse(blushBrush, px + 59, py + 36 + bob, 10, 7);

    // Leaves / stem (turnip top)
    using var stemPen  = new Pen(Color.FromArgb(180, 80, 160, 80), 2f);
    using var leaf1Pen = new Pen(Color.FromArgb(160, 100, 190, 90), 2f);
    int leafBob = sleep ? 2 : 0;
    g.DrawLine(stemPen,  px + 48, py + 18 + bob, px + 48, py + 6 + leafBob);
    g.DrawLine(leaf1Pen, px + 48, py + 12 + bob, px + 40, py + 4 + leafBob);
    g.DrawLine(leaf1Pen, px + 48, py + 12 + bob, px + 56, py + 4 + leafBob);

    // Eyes
    int eyeY = py + 30 + bob;
    if (sleep || blink)
    {
        // Closed — arc lines
        using var eyePen = new Pen(Color.FromArgb(200, 40, 30, 60), 2f);
        g.DrawArc(eyePen, px + 31, eyeY - 3, 10, 8, 0, 180);
        g.DrawArc(eyePen, px + 55, eyeY - 3, 10, 8, 0, 180);

        if (sleep)
        {
            // zzz
            using var zBrush = new SolidBrush(Color.FromArgb(160, 200, 200, 255));
            using var zFont  = new Font("Courier New", 7f, FontStyle.Bold);
            g.DrawString("z", zFont, zBrush, px + 62, py + 14 + bob);
            if (frame >= 2) g.DrawString("z", zFont, zBrush, px + 68, py + 9 + bob);
            if (frame >= 4) g.DrawString("z", zFont, zBrush, px + 74, py + 4 + bob);
        }
    }
    else if (stare)
    {
        // Wide open, large pupils
        using var whiteBrush  = new SolidBrush(Color.White);
        using var pupilBrush  = new SolidBrush(Color.FromArgb(220, 30, 20, 50));
        g.FillEllipse(whiteBrush, px + 30, eyeY - 4, 13, 13);
        g.FillEllipse(whiteBrush, px + 54, eyeY - 4, 13, 13);
        g.FillEllipse(pupilBrush, px + 33, eyeY - 1, 7, 7);
        g.FillEllipse(pupilBrush, px + 57, eyeY - 1, 7, 7);
    }
    else
    {
        // Normal eyes
        using var eyeBrush = new SolidBrush(Color.FromArgb(210, 40, 30, 60));
        int eyeH = squint ? 3 : 6;
        g.FillEllipse(eyeBrush, px + 32, eyeY - eyeH / 2, 9, eyeH);
        g.FillEllipse(eyeBrush, px + 56, eyeY - eyeH / 2, 9, eyeH);

        // Shine dot
        using var shineBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255));
        g.FillEllipse(shineBrush, px + 34, eyeY - eyeH / 2, 3, 3);
        g.FillEllipse(shineBrush, px + 58, eyeY - eyeH / 2, 3, 3);
    }

    // Mouth
    int mouthY = py + 43 + bob;
    using var mouthPen = new Pen(Color.FromArgb(180, 60, 40, 80), 1.5f);
    if (sleep || smile)
        g.DrawArc(mouthPen, px + 38, mouthY - 3, 20, 10, 0, -180);   // smile arc
    else if (worried)
        g.DrawArc(mouthPen, px + 38, mouthY + 2, 20, 8, 0, 180);     // frown arc
    else
        g.DrawLine(mouthPen, px + 38, mouthY + 2, px + 58, mouthY + 2); // flat

    // State-specific extras
    if (state is "HeatWarn" or "HeatCritical")
    {
        // Sweat drop
        using var sweatBrush = new SolidBrush(Color.FromArgb(180, 100, 180, 255));
        g.FillEllipse(sweatBrush, px + 66, py + 28 + bob, 6, 8);
        if (state == "HeatCritical" && frame >= 4)
        {
            // Extra sweat on later frames
            g.FillEllipse(sweatBrush, px + 20, py + 30 + bob, 5, 7);
        }
    }

    if (state is "CpuHigh" or "CpuHighApp")
    {
        // Frantic lines / speed marks
        using var markPen = new Pen(Color.FromArgb(120, 255, 200, 100), 1.5f);
        int offset = (frame % 2 == 0) ? 0 : 2;
        g.DrawLine(markPen, px + 16 + offset, py + 25, px + 22 + offset, py + 30);
        g.DrawLine(markPen, px + 14 + offset, py + 35, px + 20 + offset, py + 38);
    }

    if (state == "WinOpen")
    {
        // Wide eyes — already handled via stare-like eyes, add eyebrow raise
        using var browPen = new Pen(Color.FromArgb(180, 60, 40, 80), 1.5f);
        g.DrawLine(browPen, px + 31, py + 23 + bob, px + 41, py + 21 + bob);
        g.DrawLine(browPen, px + 55, py + 21 + bob, px + 65, py + 23 + bob);
    }
}

static Color Lerp(Color a, Color b, float t) => Color.FromArgb(
    (int)(a.R + (b.R - a.R) * t),
    (int)(a.G + (b.G - a.G) * t),
    (int)(a.B + (b.B - a.B) * t));
