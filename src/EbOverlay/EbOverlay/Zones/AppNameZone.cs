using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EbOverlay.Controls;

namespace EbOverlay.Zones;

/// <summary>
/// Displays the foreground window's title in the top-left zone.
/// Fades in on change, holds for a few seconds, then fades out.
/// </summary>
public class AppNameZone
{
    private readonly OutlinedTextBlock _label;
    private readonly DispatcherTimer _holdTimer;

    private static readonly Duration FadeInDuration  = new(TimeSpan.FromMilliseconds(300));
    private static readonly Duration FadeOutDuration = new(TimeSpan.FromMilliseconds(1200));
    private const double HoldSeconds = 4;

    public AppNameZone(OutlinedTextBlock label)
    {
        _label = label;

        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(HoldSeconds) };
        _holdTimer.Tick += (_, _) =>
        {
            _holdTimer.Stop();
            FadeTo(0);
        };
    }

    public void OnForegroundWindowChanged(IntPtr hwnd)
    {
        string title = GetWindowTitle(hwnd);
        if (string.IsNullOrWhiteSpace(title))
            return;

        _label.Text = title;

        _holdTimer.Stop();
        FadeTo(0.85);
        _holdTimer.Start();
    }

    private void FadeTo(double opacity)
    {
        var duration = opacity > 0 ? FadeInDuration : FadeOutDuration;
        var anim = new DoubleAnimation(opacity, duration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        _label.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        int len = GetWindowTextLength(hwnd);
        if (len == 0) return string.Empty;

        var sb = new StringBuilder(len + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);
}
