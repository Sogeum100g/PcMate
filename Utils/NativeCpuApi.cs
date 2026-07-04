using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PcMate.Utils;

internal static class NativeCpuApi
{
    public static CpuTimes GetSystemCpuTimes()
    {
        if (!GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new CpuTimes(
            ToUInt64(idleTime),
            ToUInt64(kernelTime),
            ToUInt64(userTime));
    }

    private static ulong ToUInt64(FileTime fileTime)
    {
        return ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FileTime idleTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }
}

internal sealed record CpuTimes(ulong IdleTime, ulong KernelTime, ulong UserTime)
{
    public ulong TotalTime => KernelTime + UserTime;
}
