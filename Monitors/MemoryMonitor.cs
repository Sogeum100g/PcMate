using PcMate.Models;
using PcMate.Utils;

namespace PcMate.Monitors;

public sealed class MemoryMonitor : IResourceMonitor
{
    public int GetUsagePercent(ResourceType resourceType)
    {
        return NativeMemoryApi.GetMemoryUsagePercent();
    }
}
