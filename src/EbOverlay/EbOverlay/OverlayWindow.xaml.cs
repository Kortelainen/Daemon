using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using EbOverlay.Hooks;

namespace EbOverlay;

public partial class OverlayWindow : Window
{
    private readonly FullscreenDetector _fullscreenDetector;
    private readonly DispatcherTimer _fullscreenTimer;

    public OverlayWindow()
    {
        InitializeComponent();

        _fullscreenDetector = new FullscreenDetector();
        _fullscreenTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _fullscreenTimer.Tick += OnFullscreenCheck;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StretchToFullScreen();
        SetClickThrough();
        PositionRightAlignedElements();

        var hwnd = new WindowInteropHelper(this).Handle;
        _fullscreenDetector.OwnHwnd = hwnd;

        _fullscreenTimer.Start();
    }

    private void StretchToFullScreen()
    {
        var screen = System.Windows.SystemParameters.WorkArea;
        // Use PrimaryScreenWidth/Height to cover taskbar too
        Left   = 0;
        Top    = 0;
        Width  = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
    }

    // Make the window completely click-through via Win32 extended styles
    private void SetClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            style | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_NOACTIVATE);
    }

    // Right-align elements that need canvas right-anchoring (WPF Canvas has no Right binding)
    private void PositionRightAlignedElements()
    {
        double right = Width - 24;

        Canvas.SetLeft(ClockText,   right - 60);
        Canvas.SetLeft(MetricsPanel, right - 120);
        Canvas.SetLeft(SpriteImage,  right - 112);
    }

    private void OnFullscreenCheck(object? sender, EventArgs e)
    {
        bool fullscreen = _fullscreenDetector.IsForegroundFullscreen();
        Visibility = fullscreen ? Visibility.Hidden : Visibility.Visible;
    }

    protected override void OnClosed(EventArgs e)
    {
        _fullscreenTimer.Stop();
        base.OnClosed(e);
    }
}

internal static class NativeMethods
{
    public const int GWL_EXSTYLE     = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED    = 0x00080000;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
}
