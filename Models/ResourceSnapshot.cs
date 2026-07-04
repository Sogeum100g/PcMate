namespace PcMate.Models;

public sealed class ResourceSnapshot
{
    public int MemoryUsagePercent { get; init; }

    public DateTime CollectedAt { get; init; }
}
