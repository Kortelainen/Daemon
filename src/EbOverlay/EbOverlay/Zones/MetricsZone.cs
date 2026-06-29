using System.Windows.Controls;
using System.Windows.Threading;
using EbOverlay.Services;

namespace EbOverlay.Zones;

/// <summary>
/// Binds SystemMetrics and NetworkMetrics to their display labels.
/// Highlights CPU text when load is high, hides network labels when idle.
/// </summary>
public class MetricsZone : IDisposable
{
    private readonly TextBlock _cpuText;
    private readonly TextBlock _ramText;
    private readonly TextBlock _netUpText;
    private readonly TextBlock _netDownText;
    private readonly StackPanel _netPanel;
    private readonly Dispatcher _dispatcher;

    private readonly SystemMetrics _system;
    private readonly NetworkMetrics _network;

    private const double NetIdleThresholdKBs = 1.0;
    private const double CpuHighThreshold    = 70.0;

    public MetricsZone(
        TextBlock cpuText, TextBlock ramText,
        TextBlock netUpText, TextBlock netDownText,
        StackPanel netPanel,
        Dispatcher dispatcher)
    {
        _cpuText     = cpuText;
        _ramText     = ramText;
        _netUpText   = netUpText;
        _netDownText = netDownText;
        _netPanel    = netPanel;
        _dispatcher  = dispatcher;

        _system  = new SystemMetrics();
        _network = new NetworkMetrics();

        _system.Updated  += OnSystemUpdated;
        _network.Updated += OnNetworkUpdated;
    }

    private void OnSystemUpdated(float cpu, float usedGB, float totalGB)
    {
        _dispatcher.Invoke(() =>
        {
            _cpuText.Text = $"CPU  {cpu:F0}%";

            // Highlight CPU text when load is high
            _cpuText.Foreground = cpu >= CpuHighThreshold
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x66, 0x66))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x66, 0xFF, 0x99));

            _ramText.Text = $"RAM  {usedGB:F1}/{totalGB:F0} GB";
        });
    }

    private void OnNetworkUpdated(double uploadKBs, double downloadKBs)
    {
        _dispatcher.Invoke(() =>
        {
            bool idle = uploadKBs < NetIdleThresholdKBs && downloadKBs < NetIdleThresholdKBs;
            _netPanel.Opacity = idle ? 0.15 : 0.5;

            _netUpText.Text   = $"↑  {FormatRate(uploadKBs)}";
            _netDownText.Text = $"↓  {FormatRate(downloadKBs)}";
        });
    }

    private static string FormatRate(double kbs)
    {
        if (kbs >= 1024) return $"{kbs / 1024:F1} MB/s";
        if (kbs >= 1)    return $"{kbs:F0} KB/s";
        return "—";
    }

    public void Dispose()
    {
        _system.Dispose();
        _network.Dispose();
    }
}
