using System.Runtime.InteropServices;
using System.Windows.Media;
using Image = System.Windows.Controls.Image;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EbOverlay.Services;

namespace EbOverlay.Zones;

// ── State definitions ────────────────────────────────────────────────────────

public enum SpriteState
{
    Sleep, Idle, IdleCurious, WakeUp,
    WindowSwitch, WindowOpen, WindowClose,
    Smile, Stare,
    CpuHigh, CpuHighApp,
    HeatWarn, HeatCritical
}

/// <summary>
/// Drives the sprite Image control: loads the sheet, manages the state machine,
/// steps frames, and reacts to system triggers.
/// </summary>
public sealed class SpriteZone : IDisposable
{
    // ── Config table — row order must match spritesheet.png ──────────────────
    private record StateConfig(
        SpriteState State,
        int         Row,
        int         Frames,
        int         Fps,
        bool        OneShot,   // returns to ReturnState when animation completes
        int         Priority); // higher wins; equal priority = most recent wins

    private static readonly StateConfig[] Configs =
    [
        new(SpriteState.Sleep,        0,  8,  4, false, 0),
        new(SpriteState.Idle,         1,  8,  6, false, 1),
        new(SpriteState.IdleCurious,  2,  6,  6, false, 1),
        new(SpriteState.WakeUp,       3,  6, 10, true,  2),
        new(SpriteState.WindowSwitch, 4,  5,  8, true,  3),
        new(SpriteState.WindowOpen,   5,  5,  8, true,  3),
        new(SpriteState.WindowClose,  6,  5,  8, true,  3),
        new(SpriteState.Smile,        7,  6,  8, true,  4),
        new(SpriteState.Stare,        8,  4,  4, false, 4),
        new(SpriteState.CpuHigh,      9,  8, 10, false, 5),
        new(SpriteState.CpuHighApp,  10,  6, 10, false, 5),
        new(SpriteState.HeatWarn,    11,  8,  8, false, 6),
        new(SpriteState.HeatCritical,12,  8, 12, false, 7),
    ];

    private static readonly Dictionary<SpriteState, StateConfig> ConfigMap =
        Configs.ToDictionary(c => c.State);

    // ── Thresholds ───────────────────────────────────────────────────────────
    private const float CpuHighThreshold    = 70f;
    private const float AppCpuHighThreshold = 50f;
    private const float HeatWarnThreshold   = 75f;
    private const float HeatCritThreshold   = 90f;
    private const int   IdleCuriousSeconds  = 120;
    private const int   SleepSeconds        = 600;
    private const int   StareSeconds        = 30;
    private const float SmileChancePerSec   = 0.02f;

    // ── State ────────────────────────────────────────────────────────────────
    private SpriteState _current     = SpriteState.Idle;
    private SpriteState _returnState = SpriteState.Idle;
    private int         _frame;

    // Live sensor values
    private float _sysCpu;
    private float _appCpu;
    private float _temp = -1;

    // Idle / sleep tracking
    private DateTime _lastInputTime  = DateTime.UtcNow;
    private System.Windows.Point _lastMousePos;
    private DateTime _mouseStillSince = DateTime.UtcNow;
    private bool     _wasSleeping;

    private readonly Random _rng = new();

    // ── WPF ─────────────────────────────────────────────────────────────────
    private readonly Image           _target;
    private readonly Dispatcher      _dispatcher;
    private readonly DispatcherTimer _animTimer;
    private readonly DispatcherTimer _stateTimer;
    private readonly CroppedBitmap[][] _frames;  // [row][frame]

    // ── Constructor ──────────────────────────────────────────────────────────
    public SpriteZone(Image target, Dispatcher dispatcher, string sheetPath)
    {
        _target     = target;
        _dispatcher = dispatcher;

        _frames = LoadSheet(sheetPath);

        _animTimer = new DispatcherTimer();
        _animTimer.Tick += OnAnimTick;

        _stateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _stateTimer.Tick += OnStateTick;
        _stateTimer.Start();

        ApplyState(SpriteState.Idle);
    }

    // ── Public triggers ──────────────────────────────────────────────────────

    public void OnSystemUpdated(SystemSnapshot snap)
    {
        _sysCpu = snap.CpuPercent;
        EvaluateContinuousStates();
    }

    public void OnHardwareUpdated(HardwareSnapshot snap)
    {
        if (snap.HasCpuTemp) _temp = snap.CpuTempC;
        if (snap.HasGpuTemp) _temp = Math.Max(_temp, snap.GpuTempC);
        EvaluateContinuousStates();
    }

    public void OnProcessUpdated(ProcessSnapshot? snap)
    {
        _appCpu = snap?.CpuPercent ?? 0f;
        EvaluateContinuousStates();
    }

    public void OnForegroundWindowChanged() =>
        TriggerOneShot(SpriteState.WindowSwitch);

    public void OnWindowOpened() =>
        TriggerOneShot(SpriteState.WindowOpen);

    public void OnWindowClosed() =>
        TriggerOneShot(SpriteState.WindowClose);

    // ── State machine ────────────────────────────────────────────────────────

    private void EvaluateContinuousStates()
    {
        // Highest-priority continuous state that currently applies
        SpriteState target;

        if (_temp >= HeatCritThreshold)
            target = SpriteState.HeatCritical;
        else if (_temp >= HeatWarnThreshold)
            target = SpriteState.HeatWarn;
        else if (_sysCpu >= CpuHighThreshold)
            target = SpriteState.CpuHigh;
        else if (_appCpu >= AppCpuHighThreshold)
            target = SpriteState.CpuHighApp;
        else
            return; // no override — let state timer and one-shots manage

        TryTransition(target);
    }

    private void OnStateTick(object? sender, EventArgs e)
    {
        UpdateIdleTime();

        double idleSecs = (DateTime.UtcNow - _lastInputTime).TotalSeconds;

        // Sleep / wake
        if (idleSecs >= SleepSeconds)
        {
            _wasSleeping = true;
            TryTransition(SpriteState.Sleep);
            return;
        }

        if (_wasSleeping && idleSecs < 5)
        {
            _wasSleeping = false;
            TriggerOneShot(SpriteState.WakeUp);
            return;
        }

        // Only manage lower-priority ambient states if nothing urgent is active
        var cfg = ConfigMap[_current];
        if (cfg.Priority >= 5) return;

        if (idleSecs >= IdleCuriousSeconds)
        {
            TryTransition(SpriteState.IdleCurious);
            return;
        }

        // Mouse-still stare
        var mouse = System.Windows.Forms.Cursor.Position;
        var pos   = new System.Windows.Point(mouse.X, mouse.Y);
        if (pos == _lastMousePos)
        {
            if ((DateTime.UtcNow - _mouseStillSince).TotalSeconds >= StareSeconds)
                TryTransition(SpriteState.Stare);
        }
        else
        {
            _lastMousePos    = pos;
            _mouseStillSince = DateTime.UtcNow;
            if (_current == SpriteState.Stare)
                TryTransition(SpriteState.Idle);
        }

        // Random smile while idle
        if (_current == SpriteState.Idle && _rng.NextDouble() < SmileChancePerSec)
            TriggerOneShot(SpriteState.Smile);
    }

    private void TriggerOneShot(SpriteState state)
    {
        var incoming = ConfigMap[state];
        var active   = ConfigMap[_current];

        // One-shots only interrupt if priority ≥ current, and current isn't a higher alert
        if (incoming.Priority < active.Priority && active.Priority >= 5)
            return;

        _returnState = active.OneShot ? _returnState : _current;
        _dispatcher.Invoke(() => ApplyState(state));
    }

    private void TryTransition(SpriteState state)
    {
        if (state == _current) return;

        var incoming = ConfigMap[state];
        var active   = ConfigMap[_current];

        if (incoming.Priority < active.Priority && active.Priority >= 5)
            return;

        _dispatcher.Invoke(() => ApplyState(state));
    }

    private void ApplyState(SpriteState state)
    {
        _current = state;
        _frame   = 0;

        var cfg = ConfigMap[state];
        _animTimer.Stop();
        _animTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / cfg.Fps);
        _animTimer.Start();

        ShowFrame(cfg.Row, 0);
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        var cfg = ConfigMap[_current];
        _frame++;

        if (_frame >= cfg.Frames)
        {
            if (cfg.OneShot)
            {
                // One-shot complete — return to idle or previous state
                var next = ConfigMap.ContainsKey(_returnState) ? _returnState : SpriteState.Idle;
                ApplyState(next);
                return;
            }
            _frame = 0;
        }

        ShowFrame(cfg.Row, _frame);
    }

    private void ShowFrame(int row, int frame)
    {
        if (row >= _frames.Length || frame >= _frames[row].Length)
            return;
        _target.Source = _frames[row][frame];
    }

    // ── Sprite sheet loader ──────────────────────────────────────────────────

    private static CroppedBitmap[][] LoadSheet(string path)
    {
        var src = new BitmapImage();
        src.BeginInit();
        src.UriSource        = new Uri(path, UriKind.Absolute);
        src.CacheOption      = BitmapCacheOption.OnLoad;
        src.CreateOptions    = BitmapCreateOptions.None;
        src.EndInit();
        src.Freeze();

        const int fw = 96, fh = 96;
        int cols = src.PixelWidth  / fw;
        int rows = src.PixelHeight / fh;

        var sheet = new CroppedBitmap[rows][];
        for (int r = 0; r < rows; r++)
        {
            sheet[r] = new CroppedBitmap[cols];
            for (int c = 0; c < cols; c++)
            {
                var crop = new CroppedBitmap(src,
                    new System.Windows.Int32Rect(c * fw, r * fh, fw, fh));
                crop.Freeze();
                sheet[r][c] = crop;
            }
        }
        return sheet;
    }

    // ── Idle time via Win32 ──────────────────────────────────────────────────

    private void UpdateIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (GetLastInputInfo(ref info))
        {
            uint idleMs = (uint)Environment.TickCount - info.dwTime;
            if (idleMs < 5000) // input happened recently
                _lastInputTime = DateTime.UtcNow;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    public void Dispose()
    {
        _animTimer.Stop();
        _stateTimer.Stop();
    }
}
