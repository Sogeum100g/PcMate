using PcMate.Models;

namespace PcMate.Monitors;

public interface IResourceMonitor
{
    ResourceSnapshot GetSnapshot();
}
