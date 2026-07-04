namespace PcMate.Models;

public sealed record ProcessResourceUsage(
    string DisplayName,
    int ProcessCount,
    long MemoryBytes,
    double UsagePercent = 0);
