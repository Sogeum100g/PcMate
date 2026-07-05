using PcMate.Models;

namespace PcMate.Services;

public sealed class StateClassifier
{
    private readonly Dictionary<ResourceType, ResourceThresholds> _thresholds = new()
    {
        [ResourceType.Memory] = ResourceThresholds.Default,
        [ResourceType.Cpu] = ResourceThresholds.Default,
        [ResourceType.Gpu] = ResourceThresholds.Default
    };

    public CharacterState Classify(ResourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Classify(ResourceType.Memory, snapshot.MemoryUsagePercent);
    }

    public CharacterState Classify(int usagePercent)
    {
        return Classify(usagePercent, ResourceThresholds.Default);
    }

    public CharacterState Classify(ResourceType resourceType, int usagePercent)
    {
        return Classify(usagePercent, GetThresholds(resourceType));
    }

    public ResourceThresholds GetThresholds(ResourceType resourceType)
    {
        return _thresholds.TryGetValue(resourceType, out ResourceThresholds? thresholds)
            ? thresholds
            : ResourceThresholds.Default;
    }

    public void SetThresholds(ResourceType resourceType, ResourceThresholds thresholds)
    {
        _thresholds[resourceType] = thresholds.Normalize();
    }

    private static CharacterState Classify(int usagePercent, ResourceThresholds thresholds)
    {
        return usagePercent switch
        {
            _ when usagePercent < thresholds.SittingPercent => CharacterState.Lying,
            _ when usagePercent < thresholds.WalkingPercent => CharacterState.Sitting,
            _ when usagePercent < thresholds.RunningPercent => CharacterState.Walking,
            _ => CharacterState.Running
        };
    }
}
