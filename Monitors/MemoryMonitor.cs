using PcMate.Models;
using PcMate.Utils;

namespace PcMate.Monitors;

public sealed class MemoryMonitor : IResourceMonitor
{
    public ResourceSnapshot GetSnapshot()
    {
        return new ResourceSnapshot
        {
            MemoryUsagePercent = NativeMemoryApi.GetMemoryUsagePercent(),
            CollectedAt = DateTime.Now
        };
    }
}
