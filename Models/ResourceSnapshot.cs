namespace PcMate.Models;

public sealed class ResourceSnapshot
{
    public int MemoryUsagePercent { get; init; }

    public int CpuUsagePercent { get; init; }

    public int GpuUsagePercent { get; init; }

    public int NetworkReceiveMbps { get; init; }

    public DateTime CollectedAt { get; init; }

    public int GetReading(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Memory => MemoryUsagePercent,
            ResourceType.Cpu => CpuUsagePercent,
            ResourceType.Gpu => GpuUsagePercent,
            ResourceType.Network => NetworkReceiveMbps,
            _ => MemoryUsagePercent
        };
    }
}
