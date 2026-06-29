namespace EbOverlay.Services;

/// <summary>
/// System-wide metrics snapshot. Ratios are pre-computed for bar graph rendering.
/// </summary>
public record SystemSnapshot(
    float CpuPercent,
    float RamUsedGb,
    float RamTotalGb)
{
    public float CpuRatio => CpuPercent / 100f;
    public float RamRatio => RamTotalGb > 0 ? RamUsedGb / RamTotalGb : 0f;
}

/// <summary>
/// Foreground process metrics snapshot. CpuRatio is relative to system total for bar comparison.
/// </summary>
public record ProcessSnapshot(
    string Name,
    float CpuPercent,
    long RamBytes)
{
    public float RamMb   => RamBytes / 1048576f;
    public float CpuRatio => CpuPercent / 100f;
}
