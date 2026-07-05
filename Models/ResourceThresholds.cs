namespace PcMate.Models;

public sealed class ResourceThresholds
{
    public int SittingPercent { get; init; } = 40;

    public int WalkingPercent { get; init; } = 60;

    public int RunningPercent { get; init; } = 80;

    public static ResourceThresholds Default => new();

    public ResourceThresholds Normalize()
    {
        int sittingPercent = Math.Clamp(SittingPercent, 1, 98);
        int walkingPercent = Math.Clamp(WalkingPercent, sittingPercent + 1, 99);
        int runningPercent = Math.Clamp(RunningPercent, walkingPercent + 1, 100);

        return new ResourceThresholds
        {
            SittingPercent = sittingPercent,
            WalkingPercent = walkingPercent,
            RunningPercent = runningPercent
        };
    }
}
