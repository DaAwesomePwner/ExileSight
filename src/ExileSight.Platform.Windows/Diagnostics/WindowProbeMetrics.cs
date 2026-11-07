namespace ExileSight.Platform.Windows.Diagnostics;

/// <summary>Allocation-free counters for identifying process-snapshot work in an idle run.</summary>
public static class WindowProbeMetrics
{
    private static long queries, skipped, allocated;
    public static long Queries => Interlocked.Read(ref queries);
    public static long Skipped => Interlocked.Read(ref skipped);
    public static long ManagedAllocatedBytes => Interlocked.Read(ref allocated);
    internal static void Skip() => Interlocked.Increment(ref skipped);
    internal static void Record(long bytes)
    {
        Interlocked.Increment(ref queries);
        Interlocked.Add(ref allocated, bytes);
    }
}
