using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PcMate.Models;

namespace PcMate.Services;

public sealed partial class TopProcessMonitor
{
    private const int SystemProcessInformation = 5;
    private const int StatusSuccess = 0;
    private const int NextEntryOffsetOffset = 0;
    private const int PrivateWorkingSetSizeOffset = 8;
    private const int InitialSystemProcessBufferLength = 1024 * 1024;
    private const int MaxSystemProcessQueryAttempts = 6;
    private const int UniqueProcessIdOffset64 = 80;
    private const int UniqueProcessIdOffset32 = 68;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const int StatusBufferTooSmall = unchecked((int)0xC0000023);

    private readonly Dictionary<int, CpuProcessSample> _previousCpuSamples = [];
    private DateTime _previousCpuCollectedAt = DateTime.UtcNow;
    private Dictionary<string, PerformanceCounter>? _gpuProcessCounters;

    private static readonly HashSet<string> HiddenProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Idle",
        "Memory Compression",
        "Registry",
        "Secure System",
        "System",
        "System Idle Process"
    };

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

    public IReadOnlyList<ProcessResourceUsage> GetTopProcesses(ResourceType resourceType, int count)
    {
        return resourceType switch
        {
            ResourceType.Memory => GetTopMemoryProcesses(count),
            ResourceType.Cpu => GetTopCpuProcesses(count),
            ResourceType.Gpu => GetTopGpuProcesses(count),
            _ => GetTopMemoryProcesses(count)
        };
    }

    private IReadOnlyList<ProcessResourceUsage> GetTopMemoryProcesses(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        IReadOnlyList<ProcessSample> samples = GetPrivateWorkingSetSamples();
        if (samples.Count == 0)
        {
            samples = Process.GetProcesses()
                .Select(TryReadProcess)
                .OfType<ProcessSample>()
                .ToList();
        }

        return samples
            .Where(process => ShouldShowProcess(process.ProcessName))
            .GroupBy(process => process.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProcessResourceUsage(
                GetDisplayName(group.Key),
                group.Count(),
                group.Sum(process => process.MemoryBytes)))
            .OrderByDescending(process => process.MemoryBytes)
            .Take(count)
            .ToList();
    }

    private IReadOnlyList<ProcessResourceUsage> GetTopCpuProcesses(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        DateTime collectedAt = DateTime.UtcNow;
        double elapsedSeconds = Math.Max((collectedAt - _previousCpuCollectedAt).TotalSeconds, 0.001);
        Dictionary<int, CpuProcessSample> currentSamples = [];

        List<ProcessCpuUsage> usages = Process.GetProcesses()
            .Select(process => TryReadCpuProcess(process, collectedAt))
            .OfType<CpuProcessSample>()
            .Select(sample =>
            {
                currentSamples[sample.ProcessId] = sample;
                if (!_previousCpuSamples.TryGetValue(sample.ProcessId, out CpuProcessSample? previousSample)
                    || sample.TotalProcessorTime < previousSample.TotalProcessorTime)
                {
                    return new ProcessCpuUsage(sample.ProcessName, 0);
                }

                double cpuPercent = (sample.TotalProcessorTime - previousSample.TotalProcessorTime).TotalMilliseconds
                    / (elapsedSeconds * 1000.0 * Environment.ProcessorCount)
                    * 100.0;

                return new ProcessCpuUsage(sample.ProcessName, Math.Clamp(cpuPercent, 0, 100));
            })
            .ToList();

        _previousCpuSamples.Clear();
        foreach ((int processId, CpuProcessSample sample) in currentSamples)
        {
            _previousCpuSamples[processId] = sample;
        }

        _previousCpuCollectedAt = collectedAt;

        return usages
            .Where(process => ShouldShowProcess(process.ProcessName))
            .GroupBy(process => process.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProcessResourceUsage(
                GetDisplayName(group.Key),
                group.Count(),
                0,
                group.Sum(process => process.CpuPercent)))
            .OrderByDescending(process => process.UsagePercent)
            .Take(count)
            .ToList();
    }

    private IReadOnlyList<ProcessResourceUsage> GetTopGpuProcesses(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        try
        {
            _gpuProcessCounters ??= CreateGpuProcessCounters();
            if (_gpuProcessCounters.Count == 0)
            {
                return [];
            }

            Dictionary<int, double> usageByProcessId = [];
            foreach ((string instance, PerformanceCounter counter) in _gpuProcessCounters)
            {
                if (!TryGetGpuProcessId(instance, out int processId))
                {
                    continue;
                }

                try
                {
                    usageByProcessId[processId] = usageByProcessId.GetValueOrDefault(processId) + counter.NextValue();
                }
                catch
                {
                    // GPU counters are process-backed and can vanish between refreshes.
                }
            }

            return usageByProcessId
                .Select(pair => TryCreateGpuUsage(pair.Key, pair.Value))
                .OfType<ProcessResourceUsage>()
                .Where(process => ShouldShowProcess(process.DisplayName))
                .GroupBy(process => process.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ProcessResourceUsage(
                    group.Key,
                    group.Sum(process => process.ProcessCount),
                    0,
                    group.Sum(process => process.UsagePercent)))
                .OrderByDescending(process => process.UsagePercent)
                .Take(count)
                .ToList();
        }
        catch
        {
            return [];
        }
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

    private static IReadOnlyList<ProcessSample> GetPrivateWorkingSetSamples()
    {
        Dictionary<int, string> processNames = GetProcessNamesById();
        int bufferLength = InitialSystemProcessBufferLength;

        for (int attempt = 0; attempt < MaxSystemProcessQueryAttempts; attempt++)
        {
            IntPtr buffer = IntPtr.Zero;

            try
            {
                buffer = Marshal.AllocHGlobal(bufferLength);
                int status = NtQuerySystemInformation(
                    SystemProcessInformation,
                    buffer,
                    bufferLength,
                    out int requiredLength);

                if (status == StatusSuccess)
                {
                    return ReadPrivateWorkingSetSamples(buffer, processNames);
                }

                if (status != StatusInfoLengthMismatch && status != StatusBufferTooSmall)
                {
                    return [];
                }

                bufferLength = Math.Max(bufferLength * 2, requiredLength);
            }
            catch
            {
                return [];
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        return [];
    }

    private static Dictionary<int, string> GetProcessNamesById()
    {
        Dictionary<int, string> processNames = [];
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    processNames[process.Id] = process.ProcessName;
                }
                catch
                {
                    // Processes can exit while the snapshot is being built.
                }
            }
        }

        return processNames;
    }

    private static IReadOnlyList<ProcessSample> ReadPrivateWorkingSetSamples(
        IntPtr buffer,
        IReadOnlyDictionary<int, string> processNames)
    {
        int uniqueProcessIdOffset = IntPtr.Size == 8
            ? UniqueProcessIdOffset64
            : UniqueProcessIdOffset32;
        List<ProcessSample> samples = [];
        IntPtr entry = buffer;

        while (true)
        {
            int processId = (int)Marshal.ReadIntPtr(entry, uniqueProcessIdOffset).ToInt64();
            long memoryBytes = Math.Max(0, Marshal.ReadInt64(entry, PrivateWorkingSetSizeOffset));
            if (processId > 0
                && memoryBytes > 0
                && processNames.TryGetValue(processId, out string? processName))
            {
                samples.Add(new ProcessSample(processName, memoryBytes));
            }

            int nextEntryOffset = Marshal.ReadInt32(entry, NextEntryOffsetOffset);
            if (nextEntryOffset == 0)
            {
                break;
            }

            entry = IntPtr.Add(entry, nextEntryOffset);
        }

        return samples;
    }

    private static CpuProcessSample? TryReadCpuProcess(Process process, DateTime collectedAt)
    {
        using (process)
        {
            try
            {
                return new CpuProcessSample(
                    process.Id,
                    process.ProcessName,
                    process.TotalProcessorTime,
                    collectedAt);
            }
            catch
            {
                return null;
            }
        }
    }

    private static Dictionary<string, PerformanceCounter> CreateGpuProcessCounters()
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        return category
            .GetInstanceNames()
            .Where(instance => instance.Contains("engtype_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                instance => instance,
                instance => new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true),
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryGetGpuProcessId(string instanceName, out int processId)
    {
        Match match = GpuProcessIdRegex().Match(instanceName);
        return int.TryParse(match.Groups[1].Value, out processId);
    }

    private static ProcessResourceUsage? TryCreateGpuUsage(int processId, double usagePercent)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!ShouldShowProcess(process.ProcessName))
            {
                return null;
            }

            return new ProcessResourceUsage(
                GetDisplayName(process.ProcessName),
                1,
                0,
                Math.Clamp(usagePercent, 0, 100));
        }
        catch
        {
            return null;
        }
    }

    private static bool ShouldShowProcess(string processName)
    {
        return !string.IsNullOrWhiteSpace(processName)
            && !HiddenProcessNames.Contains(processName);
    }

    private static string GetDisplayName(string processName)
    {
        return DisplayNames.TryGetValue(processName, out string? displayName)
            ? displayName
            : processName;
    }

    private sealed record ProcessSample(string ProcessName, long MemoryBytes);

    private sealed record CpuProcessSample(
        int ProcessId,
        string ProcessName,
        TimeSpan TotalProcessorTime,
        DateTime CollectedAt);

    private sealed record ProcessCpuUsage(string ProcessName, double CpuPercent);

    [GeneratedRegex(@"pid_(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GpuProcessIdRegex();

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(
        int systemInformationClass,
        IntPtr systemInformation,
        int systemInformationLength,
        out int returnLength);
}
