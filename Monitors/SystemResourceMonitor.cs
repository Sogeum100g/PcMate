using System.Diagnostics;
using PcMate.Models;
using PcMate.Utils;

namespace PcMate.Monitors;

public sealed class SystemResourceMonitor : IResourceMonitor
{
    private CpuTimes? _previousCpuTimes;
    private IReadOnlyList<PerformanceCounter>? _gpuCounters;

    public int GetUsagePercent(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Memory => NativeMemoryApi.GetMemoryUsagePercent(),
            ResourceType.Cpu => GetCpuUsagePercent(),
            ResourceType.Gpu => GetGpuUsagePercent(),
            _ => NativeMemoryApi.GetMemoryUsagePercent()
        };
    }

    private int GetCpuUsagePercent()
    {
        CpuTimes currentTimes = NativeCpuApi.GetSystemCpuTimes();
        if (_previousCpuTimes is null)
        {
            _previousCpuTimes = currentTimes;
            return 0;
        }

        CpuTimes previousTimes = _previousCpuTimes;
        _previousCpuTimes = currentTimes;

        ulong totalDelta = currentTimes.TotalTime - previousTimes.TotalTime;
        ulong idleDelta = currentTimes.IdleTime - previousTimes.IdleTime;
        if (totalDelta == 0 || idleDelta > totalDelta)
        {
            return 0;
        }

        double usagePercent = (totalDelta - idleDelta) * 100.0 / totalDelta;
        return Math.Clamp((int)Math.Round(usagePercent), 0, 100);
    }

    private int GetGpuUsagePercent()
    {
        try
        {
            _gpuCounters ??= CreateGpuCounters();
            if (_gpuCounters.Count == 0)
            {
                return 0;
            }

            float usagePercent = 0;
            foreach (PerformanceCounter counter in _gpuCounters)
            {
                try
                {
                    usagePercent += counter.NextValue();
                }
                catch
                {
                    // GPU engine counters can disappear when processes exit.
                }
            }

            return Math.Clamp((int)Math.Round(usagePercent), 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private static IReadOnlyList<PerformanceCounter> CreateGpuCounters()
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        return category
            .GetInstanceNames()
            .Where(instance => instance.Contains("engtype_", StringComparison.OrdinalIgnoreCase))
            .Select(instance => new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true))
            .ToList();
    }
}
