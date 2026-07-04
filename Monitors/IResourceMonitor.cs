using PcMate.Models;

namespace PcMate.Monitors;

public interface IResourceMonitor
{
    int GetUsagePercent(ResourceType resourceType);
}
