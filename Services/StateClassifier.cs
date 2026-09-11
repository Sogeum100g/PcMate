using PcMate.Models;

namespace PcMate.Services;

public sealed class StateClassifier
{
    private readonly Dictionary<ResourceType, ResourceThresholds> _thresholds = new()
    {
        [ResourceType.Memory] = ResourceThresholds.Default,
        [ResourceType.Cpu] = ResourceThresholds.Default,
        [ResourceType.Gpu] = ResourceThresholds.Default,
        [ResourceType.Network] = ResourceThresholds.NetworkDefault
    };

    public CharacterState Classify(ResourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Classify(ResourceType.Memory, snapshot.MemoryUsagePercent);
    }

    public CharacterState Classify(int reading)
    {
        return Classify(reading, ResourceThresholds.Default);
    }

    public CharacterState Classify(ResourceType resourceType, int reading)
    {
        return Classify(reading, GetThresholds(resourceType));
    }

    public ResourceThresholds GetThresholds(ResourceType resourceType)
    {
        return _thresholds.TryGetValue(resourceType, out ResourceThresholds? thresholds)
            ? thresholds
            : resourceType.GetDefaultThresholds();
    }

    public void SetThresholds(ResourceType resourceType, ResourceThresholds thresholds)
    {
        _thresholds[resourceType] = thresholds.Normalize(resourceType.GetThresholdMaximum());
    }

    private static CharacterState Classify(int reading, ResourceThresholds thresholds)
    {
        return reading switch
        {
            _ when reading < thresholds.SittingPercent => CharacterState.Lying,
            _ when reading < thresholds.WalkingPercent => CharacterState.Sitting,
            _ when reading < thresholds.RunningPercent => CharacterState.Walking,
            _ => CharacterState.Running
        };
    }
}
