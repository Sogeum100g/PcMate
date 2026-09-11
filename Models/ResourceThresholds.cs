namespace PcMate.Models;

public sealed class ResourceThresholds
{
    // These property names are retained for saved-settings compatibility. For Network resources,
    // the values represent Mbps rather than percentages.
    public int SittingPercent { get; init; } = 40;

    public int WalkingPercent { get; init; } = 60;

    public int RunningPercent { get; init; } = 80;

    public static ResourceThresholds Default => new();

    public static ResourceThresholds NetworkDefault => new()
    {
        SittingPercent = 3,
        WalkingPercent = 5,
        RunningPercent = 9
    };

    public ResourceThresholds Normalize(int maximumValue = 100)
    {
        maximumValue = Math.Max(maximumValue, 3);
        int sittingPercent = Math.Clamp(SittingPercent, 1, maximumValue - 2);
        int walkingPercent = Math.Clamp(WalkingPercent, sittingPercent + 1, maximumValue - 1);
        int runningPercent = Math.Clamp(RunningPercent, walkingPercent + 1, maximumValue);

        return new ResourceThresholds
        {
            SittingPercent = sittingPercent,
            WalkingPercent = walkingPercent,
            RunningPercent = runningPercent
        };
    }
}
