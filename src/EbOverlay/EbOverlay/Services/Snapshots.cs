namespace EbOverlay.Services;

public record SystemSnapshot(
    float CpuPercent,
    float RamUsedGb,
    float RamTotalGb,
    double DiskReadKBs,
    double DiskWriteKBs)
{
    public float CpuRatio  => CpuPercent / 100f;
    public float RamRatio  => RamTotalGb > 0 ? RamUsedGb / RamTotalGb : 0f;
}

/// <summary>
/// Hardware sensor snapshot from LibreHardwareMonitor.
/// Fields are -1 when the sensor is unavailable (no admin, unsupported hardware).
/// </summary>
public record HardwareSnapshot(
    float CpuTempC,
    float GpuPercent,
    float GpuTempC,
    float GpuVramUsedGb,
    float GpuVramTotalGb)
{
    public float GpuRatio     => GpuPercent / 100f;
    public float GpuVramRatio => GpuVramTotalGb > 0 ? GpuVramUsedGb / GpuVramTotalGb : 0f;
    public bool  HasCpuTemp   => CpuTempC    >= 0;
    public bool  HasGpu       => GpuPercent  >= 0;
    public bool  HasGpuTemp   => GpuTempC    >= 0;
    public bool  HasVram      => GpuVramTotalGb > 0;
}

public record ProcessSnapshot(
    string Name,
    float CpuPercent,
    long RamBytes)
{
    public float RamMb    => RamBytes / 1048576f;
    public float CpuRatio => CpuPercent / 100f;
}
