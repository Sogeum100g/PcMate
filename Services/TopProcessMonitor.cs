using System.Diagnostics;
using PcMate.Models;

namespace PcMate.Services;

public sealed class TopProcessMonitor
{
    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = "Chrome",
        ["Code"] = "Visual Studio Code",
        ["devenv"] = "Visual Studio",
        ["Discord"] = "Discord",
        ["msedge"] = "Microsoft Edge",
        ["firefox"] = "Firefox",
        ["PcMate"] = "PcMate"
    };

    public IReadOnlyList<ProcessResourceUsage> GetTopMemoryProcesses(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        return Process.GetProcesses()
            .Select(TryReadProcess)
            .OfType<ProcessSample>()
            .GroupBy(process => process.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProcessResourceUsage(
                GetDisplayName(group.Key),
                group.Count(),
                group.Sum(process => process.MemoryBytes)))
            .OrderByDescending(process => process.MemoryBytes)
            .Take(count)
            .ToList();
    }

    private static ProcessSample? TryReadProcess(Process process)
    {
        using (process)
        {
            try
            {
                return new ProcessSample(process.ProcessName, process.WorkingSet64);
            }
            catch
            {
                return null;
            }
        }
    }

    private static string GetDisplayName(string processName)
    {
        return DisplayNames.TryGetValue(processName, out string? displayName)
            ? displayName
            : processName;
    }

    private sealed record ProcessSample(string ProcessName, long MemoryBytes);
}
