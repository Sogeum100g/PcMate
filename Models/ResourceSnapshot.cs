namespace PcMate.Models;

public sealed class ResourceSnapshot
{
    public int MemoryUsagePercent { get; init; }

    public int CpuUsagePercent { get; init; }

    public int GpuUsagePercent { get; init; }

    public DateTime CollectedAt { get; init; }

    public int GetUsagePercent(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Memory => MemoryUsagePercent,
            ResourceType.Cpu => CpuUsagePercent,
            ResourceType.Gpu => GpuUsagePercent,
            _ => MemoryUsagePercent
        };
    }
}
