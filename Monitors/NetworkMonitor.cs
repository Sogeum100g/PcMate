using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace PcMate.Monitors;

// Measures actual system receive throughput from Windows interface byte counters. This is a
// sampled transfer rate, not the adapter's advertised link speed, and it does not generate any
// network traffic of its own.
public sealed class NetworkMonitor
{
    private static readonly TimeSpan MaximumSampleInterval = TimeSpan.FromSeconds(10);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private IReadOnlyDictionary<string, long>? _previousReceivedBytes;
    private TimeSpan _previousSampleTime;

    public int GetReceiveMbps()
    {
        IReadOnlyDictionary<string, long> currentReceivedBytes = ReadActiveInterfaceCounters();
        TimeSpan currentSampleTime = _clock.Elapsed;
        IReadOnlyDictionary<string, long>? previousReceivedBytes = _previousReceivedBytes;
        TimeSpan previousSampleTime = _previousSampleTime;

        _previousReceivedBytes = currentReceivedBytes;
        _previousSampleTime = currentSampleTime;

        TimeSpan elapsed = currentSampleTime - previousSampleTime;
        if (previousReceivedBytes is null || elapsed > MaximumSampleInterval)
        {
            return 0;
        }

        double deltaBytes = 0;
        foreach ((string interfaceId, long currentBytes) in currentReceivedBytes)
        {
            if (previousReceivedBytes.TryGetValue(interfaceId, out long previousBytes)
                && currentBytes >= previousBytes)
            {
                deltaBytes += currentBytes - previousBytes;
            }
        }

        return MbpsFromBytesAndElapsed(deltaBytes, elapsed);
    }

    public static int MbpsFromBytesAndElapsed(double deltaBytes, TimeSpan elapsed)
    {
        if (deltaBytes <= 0 || elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        double megabitsPerSecond = deltaBytes * 8.0 / elapsed.TotalSeconds / 1_000_000.0;
        return (int)Math.Clamp(Math.Round(megabitsPerSecond), 0, int.MaxValue);
    }

    private static IReadOnlyDictionary<string, long> ReadActiveInterfaceCounters()
    {
        var counters = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up
                || networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                    or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            try
            {
                if (!HasDefaultGateway(networkInterface))
                {
                    continue;
                }

                counters[networkInterface.Id] = networkInterface.GetIPStatistics().BytesReceived;
            }
            catch (NetworkInformationException)
            {
                // Interfaces can disappear or reset while Windows is enumerating them.
            }
        }

        return counters;
    }

    private static bool HasDefaultGateway(NetworkInterface networkInterface)
    {
        return networkInterface.GetIPProperties().GatewayAddresses.Any(gateway =>
            !gateway.Address.Equals(IPAddress.Any)
            && !gateway.Address.Equals(IPAddress.IPv6Any));
    }
}
