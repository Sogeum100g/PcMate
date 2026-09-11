using PcMate.Models;

namespace PcMate.Monitors;

public interface IResourceMonitor
{
    int GetReading(ResourceType resourceType);
}
