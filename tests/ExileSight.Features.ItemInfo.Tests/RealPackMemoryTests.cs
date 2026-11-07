using System.Diagnostics;
using System.Runtime;
using System.Text.Json;
using ExileSight.App.DataPacks;
using ExileSight.Core.DataPacks;
using ExileSight.Core.DataPacks.Loading;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
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
            // Probe only: distinguish live data from GC slack, separate from bulk-load reclamation.
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
                cpuSeconds = Environment.CpuUsage.TotalTime.TotalSeconds,
                gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2),
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
        Sample("startup prepared");
        samples.Add(new { stage = "bulk-load reclamation", milliseconds = startup.LastLoadReclaimMilliseconds });
        // Exercise the actual shared views consumed by passive features.
        _ = startup.Packs.TryGet<GuideData>("poe1/guide")!.Data;
        _ = startup.Packs.TryGet<MapModsData>("poe1/mapmods")!.Data;
        _ = startup.Packs.TryGet<EventsData>("poe1/events")!.Data;
        _ = startup.Packs.TryGet<LeaguesData>("poe1/leagues")!.Data;
        Sample("passive feature views");
        var settle = TimeSpan.FromSeconds(int.TryParse(Environment.GetEnvironmentVariable("EXILESIGHT_MEMORY_SETTLE_SECONDS"), out var seconds) ? seconds : 1);
        await Task.Delay(settle);
        var tray = Sample("tray idle");
        var item = FirstUseFixture.Parse();
        var firstUse = Stopwatch.StartNew();
        foreach (var line in item.Mods) Assert.NotEmpty(startup.Data!.MatchLine(line));
        Assert.Equal("Fecund", startup.Data!.FindMod(item, item.Mods)?.Name);
        firstUse.Stop();
        samples.Add(new { stage = "first item latency", milliseconds = firstUse.Elapsed.TotalMilliseconds });
        Sample("idle after first item");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ExileSight.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        foreach (var fixturePath in Directory.EnumerateFiles(Path.Combine(root.FullName, "tests/fixtures/poe1/items"), "*.txt", SearchOption.AllDirectories))
        {
            var fixture = ItemTextParser.Parse(await File.ReadAllTextAsync(fixturePath), Game.PoE1).Item;
            if (fixture is null) continue;
            foreach (var line in fixture.Mods) _ = startup.Data!.MatchLine(line);
            _ = new ItemInfoCalculator(startup.Data).Calculate(fixture);
        }
        Sample("after fixture work");
        await Task.Delay(settle);
        var final = Sample("idle after fixture session");
        var json = JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(json);
        if (Environment.GetEnvironmentVariable("EXILESIGHT_MEMORY_REPORT") is { } path)
            await File.WriteAllTextAsync(path, json);
        GC.KeepAlive(startup);
        Assert.True(final - initial < 50_000_000,
            $"Pack live heap delta {final - initial:N0}; tray delta {tray - initial:N0}; target < 50 MB.");
        Assert.True(firstUse.Elapsed.TotalMilliseconds < 500, $"First item use took {firstUse.Elapsed.TotalMilliseconds:F1} ms; target < 500 ms.");
    }
}
