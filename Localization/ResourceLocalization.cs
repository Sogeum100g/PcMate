using PcMate.Models;

namespace PcMate.Localization;

public static class ResourceLocalization
{
    public static string GetLocalizedName(this ResourceType resourceType)
    {
        string key = resourceType switch
        {
            ResourceType.Memory => "ResourceMemory",
            ResourceType.Cpu => "ResourceCpu",
            ResourceType.Gpu => "ResourceGpu",
            ResourceType.Network => "ResourceNetwork",
            _ => "MenuResource"
        };

        return LocalizationManager.Instance.Get(key);
    }
}
