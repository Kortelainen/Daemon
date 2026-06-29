using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EbOverlay.Controls;
using EbOverlay.Services;
using Color = System.Windows.Media.Color;

namespace EbOverlay.Zones;

public class MetricsZone : IDisposable
{
    private readonly TextBlock _cpuText;
    private readonly TextBlock _ramText;
    private readonly TextBlock _netUpText;
    private readonly TextBlock _netDownText;
    private readonly StackPanel _netPanel;
    private readonly MetricBar _cpuBar;
    private readonly MetricBar _ramBar;
    private readonly Sparkline _upSparkline;
    private readonly Sparkline _downSparkline;
    private readonly Dispatcher _dispatcher;

    private readonly SystemMetrics _system;
    private readonly NetworkMetrics _network;

    private readonly RollingBuffer _upBuffer   = new(20);
    private readonly RollingBuffer _downBuffer = new(20);

    private SystemSnapshot? _lastSystem;
    private ProcessSnapshot? _lastProcess;

    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x66, 0xFF, 0x99));
    private static readonly SolidColorBrush RedBrush   = new(Color.FromRgb(0xFF, 0x66, 0x66));

    private const float CpuHighThreshold    = 70f;
    private const double NetIdleThresholdKBs = 1.0;

    public MetricsZone(
        TextBlock cpuText, TextBlock ramText,
        TextBlock netUpText, TextBlock netDownText,
        StackPanel netPanel,
        MetricBar cpuBar, MetricBar ramBar,
        Sparkline upSparkline, Sparkline downSparkline,
        Dispatcher dispatcher)
    {
        _cpuText      = cpuText;
        _ramText      = ramText;
        _netUpText    = netUpText;
        _netDownText  = netDownText;
        _netPanel     = netPanel;
        _cpuBar       = cpuBar;
        _ramBar       = ramBar;
        _upSparkline  = upSparkline;
        _downSparkline = downSparkline;
        _dispatcher   = dispatcher;

        _system  = new SystemMetrics();
        _network = new NetworkMetrics();

        _system.Updated  += OnSystemUpdated;
        _network.Updated += OnNetworkUpdated;
    }

    public void OnProcessUpdated(ProcessSnapshot? proc)
    {
        _lastProcess = proc;
        _dispatcher.Invoke(Render);
    }

    private void OnSystemUpdated(SystemSnapshot snap)
    {
        _lastSystem = snap;
        _dispatcher.Invoke(Render);
    }

    private void Render()
    {
        if (_lastSystem is null) return;

        var sys  = _lastSystem;
        var proc = _lastProcess;

        // CPU text + bar
        if (proc is not null)
        {
            _cpuText.Text    = $"CPU  {proc.CpuPercent:F0}% | {sys.CpuPercent:F0}%";
            _cpuBar.LeftRatio  = proc.CpuRatio;
            _cpuBar.RightRatio = sys.CpuRatio;
        }
        else
        {
            _cpuText.Text    = $"CPU  {sys.CpuPercent:F0}%";
            _cpuBar.LeftRatio  = 0;
            _cpuBar.RightRatio = sys.CpuRatio;
        }
        _cpuText.Foreground = sys.CpuPercent >= CpuHighThreshold ? RedBrush : GreenBrush;

        // RAM text + bar
        if (proc is not null)
            _ramText.Text = $"RAM  {proc.RamMb:F0} MB | {sys.RamUsedGb:F1} | {sys.RamTotalGb:F0} GB";
        else
            _ramText.Text = $"RAM  {sys.RamUsedGb:F1} | {sys.RamTotalGb:F0} GB";

        _ramBar.LeftRatio  = proc is not null ? proc.RamBytes / (sys.RamTotalGb * 1073741824f) : 0;
        _ramBar.RightRatio = sys.RamRatio;
    }

    private void OnNetworkUpdated(double uploadKBs, double downloadKBs)
    {
        _upBuffer.Push(uploadKBs);
        _downBuffer.Push(downloadKBs);

        _dispatcher.Invoke(() =>
        {
            bool idle = uploadKBs < NetIdleThresholdKBs && downloadKBs < NetIdleThresholdKBs;
            _netPanel.Opacity = idle ? 0.15 : 0.5;

            _netUpText.Text   = $"↑  {FormatRate(uploadKBs)}";
            _netDownText.Text = $"↓  {FormatRate(downloadKBs)}";

            double upPeak   = Math.Max(_upBuffer.Max(),   1.0);
            double downPeak = Math.Max(_downBuffer.Max(), 1.0);
            _upSparkline.Update(_upBuffer.ToArray(),     upPeak);
            _downSparkline.Update(_downBuffer.ToArray(), downPeak);
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
