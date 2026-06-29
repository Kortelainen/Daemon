namespace EbOverlay.Zones;

/// <summary>
/// Immutable snapshot of every value that sprite rules need to evaluate.
/// Built fresh each tick by SpriteZone and passed into SpriteRules.
/// </summary>
public record SpriteContext(
    float  SysCpuPercent,    // overall CPU %
    float  AppCpuPercent,    // foreground-app CPU %
    float  MaxTempC,         // hottest sensor (CPU or GPU); -1 if unavailable
    double IdleSeconds,      // seconds since last keyboard/mouse input
    double MouseStillSeconds // seconds mouse has been stationary
);
