namespace Daemon.Zones;

/// <summary>
/// Icons that can appear in the sprite status overlay layer.
/// These represent persistent system conditions, not character animations.
/// Add new entries here; rendering is handled in SpriteStatusLayer.
/// </summary>
public enum StatusIcon
{
    SweatDrop,  // elevated heat
    Flame,      // critical heat
    Lightning,  // high CPU load
    AppBolt,    // high foreground-app CPU
}
