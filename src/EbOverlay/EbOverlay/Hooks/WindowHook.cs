using System.Runtime.InteropServices;

namespace Daemon.Hooks;

/// <summary>
/// Wraps SetWinEventHook for window lifecycle events:
///   ForegroundWindowChanged — active window changed (EVENT_SYSTEM_FOREGROUND)
///   WindowRestored          — window un-minimized / brought up (EVENT_SYSTEM_MINIMIZEEND)
///   WindowMinimized         — window minimized / put away   (EVENT_SYSTEM_MINIMIZESTART)
/// </summary>
public sealed class WindowHook : IDisposable
{
    public event Action<IntPtr>? ForegroundWindowChanged;
    public event Action?         WindowRestored;
    public event Action?         WindowMinimized;

    // Hold all delegates as fields to prevent GC collection between callbacks
    private readonly WinEventDelegate _foregroundDelegate;
    private readonly WinEventDelegate _minimizeDelegate;

    private readonly IntPtr _foregroundHook;
    private readonly IntPtr _minimizeHook;

    private const uint EVENT_SYSTEM_FOREGROUND   = 0x0003;
    private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    private const uint EVENT_SYSTEM_MINIMIZEEND   = 0x0017;
    private const uint WINEVENT_OUTOFCONTEXT      = 0x0000;

    public WindowHook()
    {
        _foregroundDelegate = OnForeground;
        _foregroundHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _foregroundDelegate,
            0, 0, WINEVENT_OUTOFCONTEXT);

        _minimizeDelegate = OnMinimize;
        _minimizeHook = SetWinEventHook(
            EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND,
            IntPtr.Zero, _minimizeDelegate,
            0, 0, WINEVENT_OUTOFCONTEXT);
    }

    private void OnForeground(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd != IntPtr.Zero)
            ForegroundWindowChanged?.Invoke(hwnd);
    }

    private void OnMinimize(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;

        if (eventType == EVENT_SYSTEM_MINIMIZEEND)
            WindowRestored?.Invoke();
        else if (eventType == EVENT_SYSTEM_MINIMIZESTART)
            WindowMinimized?.Invoke();
    }

    public void Dispose()
    {
        if (_foregroundHook != IntPtr.Zero) UnhookWinEvent(_foregroundHook);
        if (_minimizeHook   != IntPtr.Zero) UnhookWinEvent(_minimizeHook);
    }

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
}
