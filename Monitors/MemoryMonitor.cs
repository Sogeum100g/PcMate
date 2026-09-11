using PcMate.Models;
using PcMate.Utils;

namespace PcMate.Monitors;

public sealed class MemoryMonitor : IResourceMonitor
{
    public int GetReading(ResourceType resourceType)
    {
        return NativeMemoryApi.GetMemoryUsagePercent();
    }
}
