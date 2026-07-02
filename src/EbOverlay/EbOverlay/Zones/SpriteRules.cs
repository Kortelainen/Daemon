namespace Daemon.Zones;

/// <summary>
/// Declarative rule tables for the sprite state machine.
///
/// CONTINUOUS RULES — evaluated top-to-bottom every tick.
/// First matching rule wins. To change behaviour: reorder, edit thresholds, or add rows.
/// The last rule is the unconditional fallback (always true).
///
/// ONE-SHOT PRIORITIES — when an event fires (window switch, smile, wake-up…),
/// it is only allowed to interrupt the current state if its priority number is
/// greater than or equal to the active state's priority. Higher = more urgent.
/// </summary>
public static class SpriteRules
{
    // ── Thresholds — single place to tune ────────────────────────────────────

    public const float  HeatCriticalC       = 90f;
    public const float  HeatWarnC           = 75f;
    public const float  SysCpuHighPercent   = 70f;
    public const float  AppCpuHighPercent   = 50f;
    public const double SleepIdleSeconds    = 600;   // 10 min
    public const double CuriousIdleSeconds  = 120;   // 2 min
    public const double StareMouseSeconds   = 30;
    public const float  SmileChancePerTick  = 0.02f; // ~1 smile per 50 s at 1 Hz tick

    // ── Continuous rule table ─────────────────────────────────────────────────

    /// <summary>
    /// Evaluated in order every state tick. First rule whose condition is true
    /// determines the target sustained state. One-shot events bypass this table.
    /// </summary>
    public static readonly IReadOnlyList<ContinuousRule> Continuous =
    [
        new("heat-critical",  SpriteState.HeatCritical, ctx => ctx.MaxTempC  >= HeatCriticalC),
        new("heat-warn",      SpriteState.HeatWarn,     ctx => ctx.MaxTempC  >= HeatWarnC && ctx.MaxTempC < HeatCriticalC),
        new("cpu-system-high",SpriteState.CpuHigh,      ctx => ctx.SysCpuPercent >= SysCpuHighPercent),
        new("cpu-app-high",   SpriteState.CpuHighApp,   ctx => ctx.AppCpuPercent >= AppCpuHighPercent),
        new("sleeping",       SpriteState.Sleep,         ctx => ctx.IdleSeconds   >= SleepIdleSeconds),
        new("idle-curious",   SpriteState.IdleCurious,  ctx => ctx.IdleSeconds   >= CuriousIdleSeconds),
        new("mouse-stare",    SpriteState.Stare,         ctx => ctx.MouseStillSeconds >= StareMouseSeconds),
        new("default-idle",   SpriteState.Idle,          _   => true),  // unconditional fallback
    ];

    // ── One-shot priority table ───────────────────────────────────────────────

    /// <summary>
    /// Priority assigned to each state. A one-shot trigger only interrupts the
    /// current state when triggerPriority >= activePriority. Continuous states
    /// with priority >= AlertFloor cannot be interrupted by lower-priority one-shots.
    /// </summary>
    public static readonly IReadOnlyDictionary<SpriteState, int> Priority =
        new Dictionary<SpriteState, int>
        {
            [SpriteState.Sleep]         = 0,
            [SpriteState.Idle]          = 1,
            [SpriteState.IdleCurious]   = 1,
            [SpriteState.WakeUp]        = 2,
            [SpriteState.WindowSwitch]  = 3,
            [SpriteState.WindowOpen]    = 3,
            [SpriteState.WindowClose]   = 3,
            [SpriteState.Smile]         = 4,
            [SpriteState.Stare]         = 4,
            [SpriteState.CpuHigh]       = 5,
            [SpriteState.CpuHighApp]    = 5,
            [SpriteState.HeatWarn]      = 6,
            [SpriteState.HeatCritical]  = 7,
        };

    /// <summary>States at this priority or above cannot be interrupted by one-shots.</summary>
    public const int AlertFloor = 5;
}

// ── Rule type ─────────────────────────────────────────────────────────────────

/// <param name="Name">Human-readable label for debugging.</param>
/// <param name="Target">State to enter when this rule matches.</param>
/// <param name="When">Condition evaluated against the current SpriteContext.</param>
public record ContinuousRule(
    string                     Name,
    SpriteState                Target,
    Func<SpriteContext, bool>  When);
