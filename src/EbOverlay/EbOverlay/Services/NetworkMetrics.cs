using System.Net.NetworkInformation;

namespace EbOverlay.Services;

/// <summary>
/// Tracks NIC byte deltas every 2s and reports upload/download in KB/s.
/// Only counts the default non-loopback interface to avoid inflated totals.
/// </summary>
public sealed class NetworkMetrics : IDisposable
{
    public event Action<double, double>? Updated; // uploadKBs, downloadKBs

    private readonly Timer _timer;
    private long _lastBytesSent;
    private long _lastBytesReceived;
    private DateTime _lastSample = DateTime.UtcNow;

    public NetworkMetrics()
    {
        Sample(out _lastBytesSent, out _lastBytesReceived);
        _timer = new Timer(_ => Poll(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private void Poll()
    {
        Sample(out long sent, out long received);

        var now     = DateTime.UtcNow;
        double secs = (now - _lastSample).TotalSeconds;
        if (secs <= 0) return;

        double uploadKBs   = (sent     - _lastBytesSent)     / 1024.0 / secs;
        double downloadKBs = (received - _lastBytesReceived) / 1024.0 / secs;

        _lastBytesSent     = sent;
        _lastBytesReceived = received;
        _lastSample        = now;

        Updated?.Invoke(Math.Max(0, uploadKBs), Math.Max(0, downloadKBs));
    }

    private static void Sample(out long bytesSent, out long bytesReceived)
    {
        bytesSent     = 0;
        bytesReceived = 0;

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var stats      = nic.GetIPv4Statistics();
            bytesSent     += stats.BytesSent;
            bytesReceived += stats.BytesReceived;
        }
    }

    public void Dispose() => _timer.Dispose();
}
