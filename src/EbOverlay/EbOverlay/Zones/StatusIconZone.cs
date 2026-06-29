using System.Windows.Threading;
using EbOverlay.Controls;
using EbOverlay.Services;

namespace EbOverlay.Zones;

/// <summary>
/// Evaluates sensor snapshots against SpriteRules thresholds and updates
/// the SpriteStatusLayer with the currently active status icons.
///
/// Rules for which icons appear (evaluated every sensor update):
///   HeatCritical threshold  → Flame replaces SweatDrop (more severe)
///   HeatWarn threshold      → SweatDrop
///   SysCpu high             → Lightning
///   AppCpu high             → AppBolt  (shown alongside Lightning if both trigger)
///
/// Icons are ordered by urgency so the most critical sits in the top slot.
/// </summary>
public sealed class StatusIconZone
{
    private readonly SpriteStatusLayer _layer;
    private readonly Dispatcher        _dispatcher;

    private float _sysCpu;
    private float _appCpu;
    private float _maxTemp = -1;

    public StatusIconZone(SpriteStatusLayer layer, Dispatcher dispatcher)
    {
        _layer      = layer;
        _dispatcher = dispatcher;
    }

    // ── Test override ─────────────────────────────────────────────────────────

    private bool _testMode;

    /// <summary>Forces all icons visible regardless of sensor values. Toggle from tray.</summary>
    public void SetTestMode(bool enabled)
    {
        _testMode = enabled;
        Evaluate();
    }

    // ── External feed ─────────────────────────────────────────────────────────

    public void OnSystemUpdated(SystemSnapshot snap)
    {
        _sysCpu = snap.CpuPercent;
        Evaluate();
    }

    public void OnHardwareUpdated(HardwareSnapshot snap)
    {
        float t = -1f;
        if (snap.HasCpuTemp) t = Math.Max(t, snap.CpuTempC);
        if (snap.HasGpuTemp) t = Math.Max(t, snap.GpuTempC);
        _maxTemp = t;
        Evaluate();
    }

    public void OnProcessUpdated(ProcessSnapshot? snap)
    {
        _appCpu = snap?.CpuPercent ?? 0f;
        Evaluate();
    }

    // ── Rule evaluation ───────────────────────────────────────────────────────

    private void Evaluate()
    {
        IEnumerable<StatusIcon> icons = _testMode
            ? Enum.GetValues<StatusIcon>()          // show every icon when testing
            : EvaluateFromSensors();

        _dispatcher.Invoke(() => _layer.SetIcons(icons));
    }

    private IEnumerable<StatusIcon> EvaluateFromSensors()
    {
        // Ordered list — most urgent first (top slot).
        // Thresholds shared with SpriteRules so they're never duplicated.
        var icons = new List<StatusIcon>();

        if (_maxTemp >= SpriteRules.HeatCriticalC)
            icons.Add(StatusIcon.Flame);
        else if (_maxTemp >= SpriteRules.HeatWarnC)
            icons.Add(StatusIcon.SweatDrop);

        if (_sysCpu >= SpriteRules.SysCpuHighPercent)
            icons.Add(StatusIcon.Lightning);

        if (_appCpu >= SpriteRules.AppCpuHighPercent)
            icons.Add(StatusIcon.AppBolt);

        return icons;
    }
}
