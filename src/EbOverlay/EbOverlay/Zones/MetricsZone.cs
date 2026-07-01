using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EbOverlay.Controls;
using EbOverlay.Services;
using Color = System.Windows.Media.Color;

namespace EbOverlay.Zones;

public class MetricsZone : IDisposable
{
    // Text labels
    private readonly OutlinedTextBlock _cpuText, _gpuText, _vramText, _ramText, _diskText;
    private readonly OutlinedTextBlock _netUpText, _netDownText;

    // Bars
    private readonly MetricBar _cpuBar, _gpuBar, _vramBar, _ramBar;

    // Sparklines
    private readonly Sparkline _cpuSparkline, _gpuSparkline, _vramSparkline, _ramSparkline;
    private readonly Sparkline _diskReadSparkline, _diskWriteSparkline;
    private readonly Sparkline _upSparkline, _downSparkline;

    private readonly StackPanel _netPanel;
    private readonly Dispatcher _dispatcher;

    private readonly SystemMetrics   _system;
    private readonly HardwareMetrics _hardware;
    private readonly NetworkMetrics  _network;

    // Rolling history buffers (20 samples = ~40s at 2s poll)
    private readonly RollingBuffer _cpuBuf       = new(20);
    private readonly RollingBuffer _gpuBuf       = new(20);
    private readonly RollingBuffer _vramBuf      = new(20);
    private readonly RollingBuffer _ramBuf       = new(20);
    private readonly RollingBuffer _diskReadBuf  = new(20);
    private readonly RollingBuffer _diskWriteBuf = new(20);
    private readonly RollingBuffer _netUpBuf     = new(20);
    private readonly RollingBuffer _netDownBuf   = new(20);

    private SystemSnapshot?   _lastSystem;
    private HardwareSnapshot? _lastHardware;
    private ProcessSnapshot?  _lastProcess;

    // Passthrough events — subscribers (e.g. SpriteZone) can react to the same data
    public event Action<SystemSnapshot>?   SystemUpdated;
    public event Action<HardwareSnapshot>? HardwareUpdated;

    private SolidColorBrush _accentBrush = new(Color.FromRgb(0x66, 0xFF, 0x99));
    private static readonly SolidColorBrush RedBrush   = new(Color.FromRgb(0xFF, 0x66, 0x66));
    private const float CpuHighThreshold    = 70f;
    private const double NetIdleThresholdKBs = 1.0;

    public MetricsZone(
        OutlinedTextBlock cpuText, OutlinedTextBlock gpuText, OutlinedTextBlock vramText,
        OutlinedTextBlock ramText, OutlinedTextBlock diskText,
        OutlinedTextBlock netUpText, OutlinedTextBlock netDownText,
        StackPanel netPanel,
        MetricBar cpuBar, MetricBar gpuBar, MetricBar vramBar, MetricBar ramBar,
        Sparkline cpuSparkline, Sparkline gpuSparkline, Sparkline vramSparkline,
        Sparkline ramSparkline, Sparkline diskReadSparkline, Sparkline diskWriteSparkline,
        Sparkline upSparkline, Sparkline downSparkline,
        Dispatcher dispatcher)
    {
        _cpuText  = cpuText;  _gpuText  = gpuText;
        _vramText = vramText; _ramText  = ramText; _diskText = diskText;
        _netUpText = netUpText; _netDownText = netDownText;
        _netPanel  = netPanel;
        _cpuBar  = cpuBar;  _gpuBar  = gpuBar;
        _vramBar = vramBar; _ramBar  = ramBar;
        _cpuSparkline  = cpuSparkline;  _gpuSparkline  = gpuSparkline;
        _vramSparkline = vramSparkline; _ramSparkline  = ramSparkline;
        _diskReadSparkline  = diskReadSparkline;
        _diskWriteSparkline = diskWriteSparkline;
        _upSparkline   = upSparkline;
        _downSparkline = downSparkline;
        _dispatcher = dispatcher;

        _system   = new SystemMetrics();
        _hardware = new HardwareMetrics();
        _network  = new NetworkMetrics();

        _system.Updated   += s  => { _lastSystem   = s;  SystemUpdated?.Invoke(s);  _dispatcher.Invoke(Render); };
        _hardware.Updated += h  => { _lastHardware = h;  HardwareUpdated?.Invoke(h); _dispatcher.Invoke(Render); };
        _network.Updated  += OnNetworkUpdated;
    }

    public void OnProcessUpdated(ProcessSnapshot? proc)
    {
        _lastProcess = proc;
        _dispatcher.Invoke(Render);
    }

    private void Render()
    {
        var sys  = _lastSystem;
        var hw   = _lastHardware;
        var proc = _lastProcess;

        if (sys is not null)
        {
            // CPU
            string cpuLabel = proc is not null
                ? $"CPU  {proc.CpuPercent:F0}% | {sys.CpuPercent:F0}%"
                : $"CPU  {sys.CpuPercent:F0}%";
            if (hw?.HasCpuTemp == true) cpuLabel += $"   {hw.CpuTempC:F0}°C";
            _cpuText.Text = cpuLabel;
            _cpuText.Foreground = sys.CpuPercent >= CpuHighThreshold ? RedBrush : _accentBrush;
            _cpuBar.LeftRatio  = proc?.CpuRatio ?? 0;
            _cpuBar.RightRatio = sys.CpuRatio;
            _cpuBuf.Push(sys.CpuPercent);
            _cpuSparkline.Update(_cpuBuf.ToArray(), 100);

            // RAM
            string ramLabel = proc is not null
                ? $"RAM  {proc.RamMb:F0} MB | {sys.RamUsedGb:F1} | {sys.RamTotalGb:F0} GB"
                : $"RAM  {sys.RamUsedGb:F1} | {sys.RamTotalGb:F0} GB";
            _ramText.Text = ramLabel;
            _ramBar.LeftRatio  = proc is not null
                ? Math.Clamp(proc.RamBytes / (sys.RamTotalGb * 1073741824f), 0, 1) : 0;
            _ramBar.RightRatio = sys.RamRatio;
            _ramBuf.Push(sys.RamUsedGb);
            _ramSparkline.Update(_ramBuf.ToArray(), sys.RamTotalGb);

            // Disk
            _diskText.Text = $"DISK  ↑{FormatRate(sys.DiskReadKBs)}  ↓{FormatRate(sys.DiskWriteKBs)}";
            _diskReadBuf.Push(sys.DiskReadKBs);
            _diskWriteBuf.Push(sys.DiskWriteKBs);
            double diskPeak = Math.Max(Math.Max(_diskReadBuf.Max(), _diskWriteBuf.Max()), 1.0);
            _diskReadSparkline.Update(_diskReadBuf.ToArray(),   diskPeak);
            _diskWriteSparkline.Update(_diskWriteBuf.ToArray(), diskPeak);
        }

        if (hw is not null)
        {
            // GPU
            string gpuLabel = hw.HasGpu ? $"GPU  {hw.GpuPercent:F0}%" : "GPU  —";
            if (hw.HasGpuTemp) gpuLabel += $"   {hw.GpuTempC:F0}°C";
            _gpuText.Text = gpuLabel;
            _gpuBar.LeftRatio  = 0;
            _gpuBar.RightRatio = hw.GpuRatio;
            _gpuBuf.Push(hw.HasGpu ? hw.GpuPercent : 0);
            _gpuSparkline.Update(_gpuBuf.ToArray(), 100);

            // VRAM
            if (hw.HasVram)
            {
                _vramText.Text = $"VRAM  {hw.GpuVramUsedGb:F1} | {hw.GpuVramTotalGb:F0} GB";
                _vramBar.LeftRatio  = 0;
                _vramBar.RightRatio = hw.GpuVramRatio;
                _vramBuf.Push(hw.GpuVramUsedGb);
                _vramSparkline.Update(_vramBuf.ToArray(), hw.GpuVramTotalGb);
            }
            else
            {
                _vramText.Text = "VRAM  —";
            }
        }
    }

    private void OnNetworkUpdated(double uploadKBs, double downloadKBs)
    {
        _netUpBuf.Push(uploadKBs);
        _netDownBuf.Push(downloadKBs);

        _dispatcher.Invoke(() =>
        {
            bool idle = uploadKBs < NetIdleThresholdKBs && downloadKBs < NetIdleThresholdKBs;
            _netPanel.Opacity = idle ? 0.15 : 0.55;

            _netUpText.Text   = $"↑  {FormatRate(uploadKBs)}";
            _netDownText.Text = $"↓  {FormatRate(downloadKBs)}";

            double upPeak   = Math.Max(_netUpBuf.Max(),   1.0);
            double downPeak = Math.Max(_netDownBuf.Max(), 1.0);
            _upSparkline.Update(_netUpBuf.ToArray(),   upPeak);
            _downSparkline.Update(_netDownBuf.ToArray(), downPeak);
        });
    }

    private static string FormatRate(double kbs)
    {
        if (kbs >= 1024) return $"{kbs / 1024:F1} MB/s";
        if (kbs >= 1)    return $"{kbs:F0} KB/s";
        return "—";
    }

    public void SetAccentColor(Color c)
    {
        _accentBrush = new SolidColorBrush(c);
        foreach (var bar in new[] { _cpuBar, _gpuBar, _vramBar, _ramBar })
            bar.SetAccentColor(c);
        foreach (var spark in new[] { _cpuSparkline, _gpuSparkline, _vramSparkline, _ramSparkline,
                                      _diskReadSparkline, _diskWriteSparkline, _upSparkline, _downSparkline })
            spark.SetAccentColor(c);
    }

    public void Dispose()
    {
        _system.Dispose();
        _hardware.Dispose();
        _network.Dispose();
    }
}
