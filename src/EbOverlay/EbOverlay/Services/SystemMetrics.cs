using System.Diagnostics;
using System.Runtime.InteropServices;
using Timer = System.Threading.Timer;

namespace EbOverlay.Services;

/// <summary>
/// Polls CPU usage and RAM on a background timer.
/// Consumers subscribe to Updated for fresh readings every 2s.
/// </summary>
public sealed class SystemMetrics : IDisposable
{
    public event Action<SystemSnapshot>? Updated;

    private readonly PerformanceCounter _cpuCounter;
    private readonly Timer _timer;

    public SystemMetrics()
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _cpuCounter.NextValue(); // first call always returns 0 — discard it

        _timer = new Timer(_ => Poll(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private void Poll()
    {
        float cpu = _cpuCounter.NextValue();

        GetMemoryStatus(out ulong totalBytes, out ulong availBytes);
        float totalGB = totalBytes / 1073741824f;
        float usedGB  = (totalBytes - availBytes) / 1073741824f;

        Updated?.Invoke(new SystemSnapshot(cpu, usedGB, totalGB));
    }

    private static void GetMemoryStatus(out ulong totalBytes, out ulong availBytes)
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref status);
        totalBytes = status.ullTotalPhys;
        availBytes = status.ullAvailPhys;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _cpuCounter.Dispose();
    }

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint  dwLength;
        public uint  dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
