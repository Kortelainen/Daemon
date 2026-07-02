using System.Diagnostics;
using System.Runtime.InteropServices;
using Timer = System.Threading.Timer;

namespace Daemon.Services;

/// <summary>
/// Samples CPU and RAM for the foreground process group — all processes sharing
/// the same name as the foreground window's process are summed together.
/// This correctly accounts for multi-process apps like browsers and Electron apps.
/// </summary>
public sealed class ForegroundProcessMetrics : IDisposable
{
    public event Action<ProcessSnapshot?>? Updated;

    private readonly Timer _timer;
    private int _pid;
    private string? _processName;
    private TimeSpan _lastCpuTime;
    private DateTime _lastSample = DateTime.UtcNow;
    private static readonly int CoreCount = Environment.ProcessorCount;

    public ForegroundProcessMetrics()
    {
        _timer = new Timer(_ => Poll(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public void OnForegroundWindowChanged(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        _pid = (int)pid;

        try
        {
            _processName = Process.GetProcessById(_pid).ProcessName;
        }
        catch
        {
            _processName = null;
        }

        _lastCpuTime = TimeSpan.Zero;
        _lastSample  = DateTime.UtcNow;
    }

    private void Poll()
    {
        if (_pid == 0 || _processName is null) return;

        try
        {
            // Sum all processes with the same name as the foreground process
            var group = Process.GetProcessesByName(_processName);
            if (group.Length == 0) { Updated?.Invoke(null); return; }

            var now      = DateTime.UtcNow;
            double elapsed = (now - _lastSample).TotalSeconds;

            long   totalRam = 0;
            var    totalCpu = TimeSpan.Zero;

            foreach (var p in group)
            {
                try
                {
                    totalRam += p.WorkingSet64;
                    totalCpu += p.TotalProcessorTime;
                }
                catch { /* process may have exited mid-iteration */ }
                finally { p.Dispose(); }
            }

            float cpuPercent = 0f;
            if (_lastCpuTime != TimeSpan.Zero && elapsed > 0)
            {
                double delta = (totalCpu - _lastCpuTime).TotalSeconds;
                cpuPercent = (float)(delta / elapsed / CoreCount * 100.0);
                cpuPercent = Math.Clamp(cpuPercent, 0f, 100f);
            }

            _lastCpuTime = totalCpu;
            _lastSample  = now;

            Updated?.Invoke(new ProcessSnapshot(_processName, cpuPercent, totalRam));
        }
        catch
        {
            Updated?.Invoke(null);
        }
    }

    public void Dispose() => _timer.Dispose();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);
}
