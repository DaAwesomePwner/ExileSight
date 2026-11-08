using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.ProcessStatus;

namespace ExileSight.Platform.Windows.Diagnostics;

public readonly record struct ResourceSample(long Timestamp, TimeSpan CpuTime, long PrivateBytes);

/// <summary>Own-process only: no PID or handle parameter, and never opens a process handle.</summary>
public static class OwnProcessResources
{
    public static unsafe ResourceSample Read()
    {
        var counters = new PROCESS_MEMORY_COUNTERS_EX { cb = (uint)sizeof(PROCESS_MEMORY_COUNTERS_EX) };
        if (!PInvoke.GetProcessMemoryInfo(PInvoke.GetCurrentProcess(),
            (PROCESS_MEMORY_COUNTERS*)&counters, counters.cb))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(Stopwatch.GetTimestamp(), Environment.CpuUsage.TotalTime, checked((long)counters.PrivateUsage));
    }

    // One fully busy core = 100%; intentionally NOT divided by logical processor count.
    public static double CpuPercent(ResourceSample previous, ResourceSample current) =>
        100 * (current.CpuTime - previous.CpuTime).TotalSeconds /
        Stopwatch.GetElapsedTime(previous.Timestamp, current.Timestamp).TotalSeconds;
}
