namespace PcMate.Models;

public enum ResourceType
{
    Memory,
    Cpu,
    Gpu,
    Network
}

public static class ResourceTypeExtensions
{
    public static string FormatReading(this ResourceType resourceType, int value)
    {
        return resourceType == ResourceType.Network
            ? $"{value} Mbps"
            : $"{value}%";
    }

    public static string GetThresholdUnit(this ResourceType resourceType)
    {
        return resourceType == ResourceType.Network ? "Mbps" : "%";
    }

    public static int GetThresholdMaximum(this ResourceType resourceType)
    {
        return resourceType == ResourceType.Network ? 500 : 100;
    }

    public static ResourceThresholds GetDefaultThresholds(this ResourceType resourceType)
    {
        return resourceType == ResourceType.Network
            ? ResourceThresholds.NetworkDefault
            : ResourceThresholds.Default;
    }
}
