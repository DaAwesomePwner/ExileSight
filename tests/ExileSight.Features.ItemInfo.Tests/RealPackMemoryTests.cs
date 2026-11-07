using System.Diagnostics;
using System.Runtime;
using System.Text.Json;
using ExileSight.App.DataPacks;
using ExileSight.Core.DataPacks;
using ExileSight.Core.DataPacks.Loading;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class RealPacksFactAttribute : FactAttribute
{
    public RealPacksFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EXILESIGHT_REAL_PACKS")))
            Skip = "Set EXILESIGHT_REAL_PACKS to the verified shipping pack directory for the memory probe.";
    }
}

public sealed class RealPackMemoryTests(ITestOutputHelper output)
{
    [RealPacksFact]
    public async Task MeasureProductionStartupWithShippingPacks()
    {
        var samples = new List<object>();
        var clock = Stopwatch.StartNew();
        long Sample(string stage)
        {
            var uncollected = GC.GetTotalMemory(false);
            var committedBeforeCollection = GC.GetGCMemoryInfo().TotalCommittedBytes;
            // Probe only: distinguish live data from GC slack; never force GC in production.
            if (Environment.GetEnvironmentVariable("EXILESIGHT_MEMORY_NO_FORCED_GC") != "1")
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            }
            var retained = GC.GetTotalMemory(false);
            var gc = GC.GetGCMemoryInfo();
            using var process = Process.GetCurrentProcess();
            samples.Add(new { stage, uncollected, committedBeforeCollection, retained, gc.TotalCommittedBytes, gc.HeapSizeBytes,
                allocated = GC.GetTotalAllocatedBytes(), process.WorkingSet64,
                seconds = clock.Elapsed.TotalSeconds, GCSettings.IsServerGC,
                forcedCollection = Environment.GetEnvironmentVariable("EXILESIGHT_MEMORY_NO_FORCED_GC") != "1",
                conserveMemory = Environment.GetEnvironmentVariable("DOTNET_GCConserveMemory") ?? "runtime default" });
            return retained;
        }
        var initial = Sample("before");
        using var transport = new GitHubTransport("memory-probe");
        var loader = new PackLoader(Environment.GetEnvironmentVariable("EXILESIGHT_REAL_PACKS")!, transport);
        var startup = new PackStartup(async (id, token) =>
        {
            var loaded = await loader.ReadLocalAsync(id, token);
            Assert.NotNull(loaded.Pack);
            Sample("read " + id);
            return (loaded.Pack, "fixture");
        }, (_, _) => Task.CompletedTask);
        await startup.LoadAsync("probe", default);
        var loadedBytes = Sample("startup prepared");
        // Exercise the actual shared views consumed by passive features.
        _ = startup.Packs.TryGet<GuideData>("poe1/guide")!.Data;
        _ = startup.Packs.TryGet<MapModsData>("poe1/mapmods")!.Data;
        _ = startup.Packs.TryGet<EventsData>("poe1/events")!.Data;
        _ = startup.Packs.TryGet<LeaguesData>("poe1/leagues")!.Data;
        Sample("passive feature views");
        await Task.Delay(TimeSpan.FromSeconds(int.TryParse(Environment.GetEnvironmentVariable("EXILESIGHT_MEMORY_SETTLE_SECONDS"), out var seconds) ? seconds : 1));
        var final = Sample("settled");
        var json = JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(json);
        if (Environment.GetEnvironmentVariable("EXILESIGHT_MEMORY_REPORT") is { } path)
            await File.WriteAllTextAsync(path, json);
        GC.KeepAlive(startup);
        Assert.True(final - initial < 80_000_000,
            $"Pack live heap delta {final - initial:N0}; prepared delta {loadedBytes - initial:N0}; target < 80 MB.");
    }
}
