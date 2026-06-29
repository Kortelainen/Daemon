using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EbOverlay.Services;
using Image = System.Windows.Controls.Image;

namespace EbOverlay.Zones;

// ── Animation config — one row per state, order matches spritesheet rows ──────

file record AnimConfig(
    SpriteState State,
    int         Row,
    int         Frames,
    int         Fps,
    bool        OneShot);   // true = plays once, then falls back to best continuous state

file static class AnimConfigs
{
    public static readonly IReadOnlyDictionary<SpriteState, AnimConfig> All =
        new AnimConfig[]
        {
            new(SpriteState.Sleep,         0,  8,  4, false),
            new(SpriteState.Idle,          1,  8,  6, false),
            new(SpriteState.IdleCurious,   2,  6,  6, false),
            new(SpriteState.WakeUp,        3,  6, 10, true),
            new(SpriteState.WindowSwitch,  4,  5,  8, true),
            new(SpriteState.WindowOpen,    5,  5,  8, true),
            new(SpriteState.WindowClose,   6,  5,  8, true),
            new(SpriteState.Smile,         7,  6,  8, true),
            new(SpriteState.Stare,         8,  4,  4, false),
            new(SpriteState.CpuHigh,       9,  8, 10, false),
            new(SpriteState.CpuHighApp,   10,  6, 10, false),
            new(SpriteState.HeatWarn,     11,  8,  8, false),
            new(SpriteState.HeatCritical, 12,  8, 12, false),
        }.ToDictionary(c => c.State);
}

// ── SpriteZone ────────────────────────────────────────────────────────────────

/// <summary>
/// Drives the sprite Image control.
/// Decision logic lives in SpriteRules; this class only manages animation
/// mechanics and routes external triggers through the priority gate.
/// </summary>
public sealed class SpriteZone : IDisposable
{
    // ── Live sensor snapshot (updated from service callbacks) ─────────────────
    private float  _sysCpu;
    private float  _appCpu;
    private float  _maxTemp = -1;

    // ── Input tracking ────────────────────────────────────────────────────────
    private DateTime           _lastInputAt     = DateTime.UtcNow;
    private DateTime           _mouseStillSince = DateTime.UtcNow;
    private System.Windows.Point _lastMousePos;
    private bool               _wasSleeping;

    // ── Animation state ───────────────────────────────────────────────────────
    private SpriteState        _current = SpriteState.Idle;
    private int                _frame;

    private readonly Random          _rng  = new();
    private readonly Image           _target;
    private readonly DispatcherTimer _animTimer;
    private readonly DispatcherTimer _stateTick;
    private readonly CroppedBitmap[][] _frames;   // [row][frame], pre-cropped at startup

    // ── Construction ──────────────────────────────────────────────────────────

    public SpriteZone(Image target, Dispatcher dispatcher)
    {
        _target = target;
        _frames = LoadSheet();

        _animTimer = new DispatcherTimer();
        _animTimer.Tick += OnAnimTick;

        _stateTick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _stateTick.Tick += (_, _) => dispatcher.Invoke(OnStateTick);
        _stateTick.Start();

        ApplyState(SpriteState.Idle);
    }

    // ── External triggers (called from OverlayWindow) ─────────────────────────

    public void OnSystemUpdated(SystemSnapshot snap)
    {
        _sysCpu = snap.CpuPercent;
    }

    public void OnHardwareUpdated(HardwareSnapshot snap)
    {
        float t = -1f;
        if (snap.HasCpuTemp) t = Math.Max(t, snap.CpuTempC);
        if (snap.HasGpuTemp) t = Math.Max(t, snap.GpuTempC);
        _maxTemp = t;
    }

    public void OnProcessUpdated(ProcessSnapshot? snap)
    {
        _appCpu = snap?.CpuPercent ?? 0f;
    }

    public void OnForegroundWindowChanged() => TryOneShot(SpriteState.WindowSwitch);
    public void OnWindowOpened()            => TryOneShot(SpriteState.WindowOpen);
    public void OnWindowClosed()            => TryOneShot(SpriteState.WindowClose);

    // ── Periodic evaluation ───────────────────────────────────────────────────

    private void OnStateTick()
    {
        RefreshIdleTime();
        CheckWakeUp();

        double idle       = (DateTime.UtcNow - _lastInputAt).TotalSeconds;
        double mouseStill = GetMouseStillSeconds();

        var ctx    = new SpriteContext(_sysCpu, _appCpu, _maxTemp, idle, mouseStill);
        var winner = SpriteRules.Continuous.FirstOrDefault(r => r.When(ctx));
        if (winner is null) return;

        // For one-shot states already playing, only interrupt with continuous rules
        // if the current animation has finished (handled in OnAnimTick).
        // For looping states, transition freely.
        var activeCfg = AnimConfigs.All[_current];
        if (!activeCfg.OneShot)
            TryContinuousTransition(winner.Target);

        // Random smile — only fires from idle, as a one-shot
        if (_current == SpriteState.Idle && _rng.NextDouble() < SpriteRules.SmileChancePerTick)
            TryOneShot(SpriteState.Smile);
    }

    // ── State transition helpers ──────────────────────────────────────────────

    /// <summary>
    /// Apply a continuous (looping) state. Lower priority cannot override a higher-priority
    /// alert state, preventing stress animations from being cut by ambient ones.
    /// </summary>
    private void TryContinuousTransition(SpriteState next)
    {
        if (next == _current) return;

        int currentPri = SpriteRules.Priority[_current];
        int nextPri    = SpriteRules.Priority[next];

        // A lower-priority continuous state cannot interrupt an active alert
        if (nextPri < currentPri && currentPri >= SpriteRules.AlertFloor)
            return;

        ApplyState(next);
    }

    /// <summary>
    /// Fire a one-shot state (event-driven: window switch, smile, wake-up).
    /// Blocked when the active state is a high-priority alert.
    /// </summary>
    private void TryOneShot(SpriteState oneShot)
    {
        int currentPri = SpriteRules.Priority[_current];
        int triggerPri = SpriteRules.Priority[oneShot];

        if (triggerPri < currentPri && currentPri >= SpriteRules.AlertFloor)
            return;

        ApplyState(oneShot);
    }

    // ── Animation mechanics ───────────────────────────────────────────────────

    private void ApplyState(SpriteState state)
    {
        _current = state;
        _frame   = 0;

        var cfg = AnimConfigs.All[state];
        _animTimer.Stop();
        _animTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / cfg.Fps);
        _animTimer.Start();

        ShowFrame(cfg.Row, 0);
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        var cfg = AnimConfigs.All[_current];
        _frame++;

        if (_frame >= cfg.Frames)
        {
            if (cfg.OneShot)
            {
                // One-shot finished — re-evaluate rules to find natural current state
                FallBackToContinuousState();
                return;
            }
            _frame = 0;
        }

        ShowFrame(cfg.Row, _frame);
    }

    private void FallBackToContinuousState()
    {
        double idle       = (DateTime.UtcNow - _lastInputAt).TotalSeconds;
        double mouseStill = GetMouseStillSeconds();
        var ctx    = new SpriteContext(_sysCpu, _appCpu, _maxTemp, idle, mouseStill);
        var winner = SpriteRules.Continuous.FirstOrDefault(r => r.When(ctx));
        ApplyState(winner?.Target ?? SpriteState.Idle);
    }

    private void ShowFrame(int row, int frame)
    {
        if (row < _frames.Length && frame < _frames[row].Length)
            _target.Source = _frames[row][frame];
    }

    // ── Input / idle helpers ──────────────────────────────────────────────────

    private void RefreshIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (GetLastInputInfo(ref info))
        {
            uint idleMs = (uint)Environment.TickCount - info.dwTime;
            if (idleMs < 2000)
                _lastInputAt = DateTime.UtcNow;
        }
    }

    private void CheckWakeUp()
    {
        bool sleeping = (DateTime.UtcNow - _lastInputAt).TotalSeconds >= SpriteRules.SleepIdleSeconds;
        if (_wasSleeping && !sleeping)
        {
            _wasSleeping = false;
            TryOneShot(SpriteState.WakeUp);
        }
        _wasSleeping = sleeping;
    }

    private double GetMouseStillSeconds()
    {
        var raw = System.Windows.Forms.Cursor.Position;
        var pos = new System.Windows.Point(raw.X, raw.Y);

        if (pos != _lastMousePos)
        {
            _lastMousePos    = pos;
            _mouseStillSince = DateTime.UtcNow;
        }

        return (DateTime.UtcNow - _mouseStillSince).TotalSeconds;
    }

    // ── Sprite sheet loader ───────────────────────────────────────────────────

    private static CroppedBitmap[][] LoadSheet()
    {
        // Loaded from the WPF resource embedded in the assembly — works in both
        // debug (file on disk) and release (single-file publish) builds.
        var src = new BitmapImage();
        src.BeginInit();
        src.UriSource     = new Uri("pack://application:,,,/Sprites/spritesheet.png");
        src.CacheOption   = BitmapCacheOption.OnLoad;
        src.CreateOptions = BitmapCreateOptions.None;
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

    // ── Win32 ─────────────────────────────────────────────────────────────────

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
        _stateTick.Stop();
    }
}
