using System.Runtime.InteropServices;
using System.Windows;

namespace Daemon.Hooks;

/// <summary>
/// Detects whether the current foreground window is covering the entire screen
/// (fullscreen game, video player, etc.) so the overlay can hide itself.
/// Set OwnHwnd so the overlay never mistakes itself for a fullscreen window.
/// </summary>
public class FullscreenDetector
{
    public IntPtr OwnHwnd { get; set; }

    public bool IsForegroundFullscreen()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        // Never treat our own overlay or the desktop/shell as fullscreen
        if (hwnd == OwnHwnd)
            return false;
        if (hwnd == NativeMethods.GetDesktopWindow())
            return false;
        if (hwnd == NativeMethods.GetShellWindow())
            return false;

        NativeMethods.GetWindowRect(hwnd, out RECT windowRect);

        int screenW = (int)SystemParameters.PrimaryScreenWidth;
        int screenH = (int)SystemParameters.PrimaryScreenHeight;

        return windowRect.Left   <= 0
            && windowRect.Top    <= 0
            && windowRect.Right  >= screenW
            && windowRect.Bottom >= screenH;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
}
