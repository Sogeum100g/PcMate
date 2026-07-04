using PcMate.Models;

namespace PcMate.Services;

public sealed class StateClassifier
{
    public CharacterState Classify(ResourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.MemoryUsagePercent switch
        {
            < 40 => CharacterState.Lying,
            < 60 => CharacterState.Sitting,
            < 80 => CharacterState.Walking,
            _ => CharacterState.Running
        };
    }
}
