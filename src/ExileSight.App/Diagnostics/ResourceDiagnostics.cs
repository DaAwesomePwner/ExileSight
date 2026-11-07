using ExileSight.Core.Diagnostics;
using ExileSight.Platform.Windows.Diagnostics;

namespace ExileSight.App.Diagnostics;

/// <summary>Sample only when the existing visible diagnostics panel requests a snapshot.</summary>
public sealed class ResourceDiagnostics : IDebugSection
{
    private ResourceSample? previous;
    public string Title => "Own-process resources";

    public IReadOnlyList<KeyValuePair<string, string>> Snapshot()
    {
        var current = OwnProcessResources.Read();
        var cpu = previous is { } last ? $"{OwnProcessResources.CpuPercent(last, current):F2}%" : "Sampling…";
        previous = current;
        return [new("CPU (100% = one core)", cpu),
            new("Private memory", $"{current.PrivateBytes / 1_000_000d:F1} MB"),
            new("Managed heap (approximate)", $"{GC.GetTotalMemory(false) / 1_000_000d:F1} MB"),
            new("GC committed (last collection)", $"{GC.GetGCMemoryInfo().TotalCommittedBytes / 1_000_000d:F1} MB"),
            new("Idle budget", "60 s average CPU < 1%; peak sampled private memory < 200 MB")];
    }
}
