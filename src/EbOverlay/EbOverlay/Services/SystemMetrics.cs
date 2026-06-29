using System.Diagnostics;
using System.Runtime.InteropServices;
using Timer = System.Threading.Timer;

namespace EbOverlay.Services;

public sealed class SystemMetrics : IDisposable
{
    public event Action<SystemSnapshot>? Updated;

    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _diskReadCounter;
    private readonly PerformanceCounter _diskWriteCounter;
    private readonly Timer _timer;

    public SystemMetrics()
    {
        _cpuCounter       = new PerformanceCounter("Processor",    "% Processor Time",  "_Total");
        _diskReadCounter  = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec",  "_Total");
        _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

        // Discard first sample — PerformanceCounter always returns 0 on first call
        _cpuCounter.NextValue();
        _diskReadCounter.NextValue();
        _diskWriteCounter.NextValue();

        _timer = new Timer(_ => Poll(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private void Poll()
    {
        float cpu        = _cpuCounter.NextValue();
        double diskReadKBs  = _diskReadCounter.NextValue()  / 1024.0;
        double diskWriteKBs = _diskWriteCounter.NextValue() / 1024.0;

        GetMemoryStatus(out ulong totalBytes, out ulong availBytes);
        float totalGB = totalBytes / 1073741824f;
        float usedGB  = (totalBytes - availBytes) / 1073741824f;

        Updated?.Invoke(new SystemSnapshot(cpu, usedGB, totalGB, diskReadKBs, diskWriteKBs));
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
        _diskReadCounter.Dispose();
        _diskWriteCounter.Dispose();
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
