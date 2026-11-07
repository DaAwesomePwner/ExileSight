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

[CollectionDefinition("Real pack probes", DisableParallelization = true)]
public sealed class RealPackProbeCollection;

[Collection("Real pack probes")]
public sealed class RealPackMemoryTests(ITestOutputHelper output)
{
    [RealPacksFact]
    public async Task MeasureProductionStartupWithShippingPacks()
    {
        var samples = new List<object>();
        var clock = Stopwatch.StartNew();
        long Sample(string stage, bool collect = false)
        {
            var uncollected = GC.GetTotalMemory(false);
            var committedBeforeCollection = GC.GetGCMemoryInfo().TotalCommittedBytes;
            // Only the final live-data measurement forces GC. Natural session observations
            // include production bulk-load reclamation but add no diagnostic collections.
            if (collect)
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            }
            var retained = GC.GetTotalMemory(false);
            var gc = GC.GetGCMemoryInfo();
            using var process = Process.GetCurrentProcess();
            samples.Add(new { stage, uncollected, committedBeforeCollection, retained, gc.TotalCommittedBytes,
                gc.HeapSizeBytes, gc.FragmentedBytes, allocated = GC.GetTotalAllocatedBytes(), process.WorkingSet64,
                cpuSeconds = Environment.CpuUsage.TotalTime.TotalSeconds,
                gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2),
                seconds = clock.Elapsed.TotalSeconds, GCSettings.IsServerGC, forcedCollection = collect,
                conserveMemory = Environment.GetEnvironmentVariable("DOTNET_GCConserveMemory") ?? "runtime default" });
            return retained;
        }
        var initial = Sample("before");
        using var transport = new GitHubTransport("memory-probe");
        var loader = new PackLoader(Environment.GetEnvironmentVariable("EXILESIGHT_REAL_PACKS")!, transport);
        var refreshReads = 0;
        var startup = new PackStartup(async (id, token) =>
        {
            var loaded = await loader.ReadLocalAsync(id, token);
            Assert.NotNull(loaded.Pack);
            return (loaded.Pack, "fixture");
        }, async (id, token) =>
        {
            // Offline same-version refresh: exercise verified local fallback reads without
            // downloading or modifying the real packs. RefreshAsync then republishes them.
            Assert.NotNull((await loader.ReadLocalAsync(id, token)).Pack);
            refreshReads++;
        });
        void Reclamation(string stage) => samples.Add(new
        {
            stage, milliseconds = startup.LastLoadReclaimMilliseconds,
            onThreadPool = startup.LastLoadReclaimOnThreadPool, committedAfterLoad = startup.LastLoadCommittedBytes
        });
        await startup.LoadAsync("probe", default);
        Sample("startup prepared");
        var loadCommitted = startup.LastLoadCommittedBytes;
        Reclamation("bulk-load reclamation");
        Assert.True(startup.LastLoadReclaimOnThreadPool);
        _ = startup.Packs.TryGet<GuideData>("poe1/guide")!.Data;
        _ = startup.Packs.TryGet<MapModsData>("poe1/mapmods")!.Data;
        _ = startup.Packs.TryGet<EventsData>("poe1/events")!.Data;
        _ = startup.Packs.TryGet<LeaguesData>("poe1/leagues")!.Data;
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
        async Task CheckItems()
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root.FullName, "tests/fixtures/poe1/items"), "*.txt", SearchOption.AllDirectories))
            {
                var fixture = ItemTextParser.Parse(await File.ReadAllTextAsync(path), Game.PoE1).Item;
                if (fixture is null) continue;
                foreach (var line in fixture.Mods) _ = startup.Data!.MatchLine(line);
                _ = new ItemInfoCalculator(startup.Data).Calculate(fixture);
            }
        }
        await CheckItems();
        await Task.Delay(settle);
        Sample("idle after item session (natural)");
        var itemCommitted = GC.GetGCMemoryInfo().TotalCommittedBytes;
        await startup.RefreshAsync(default);
        Assert.Equal(PackStartup.PackIds.Count, refreshReads);
        Reclamation("refresh reclamation");
        var refreshLoadCommitted = startup.LastLoadCommittedBytes;
        Assert.True(startup.LastLoadReclaimOnThreadPool);
        await CheckItems();
        await Task.Delay(settle);
        Sample("idle after refresh session (natural)");
        var refreshCommitted = GC.GetGCMemoryInfo().TotalCommittedBytes;
        var final = Sample("idle after fixture session", collect: true);
        var json = JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(json);
        if (Environment.GetEnvironmentVariable("EXILESIGHT_MEMORY_REPORT") is { } path)
            await File.WriteAllTextAsync(path, json);
        GC.KeepAlive(startup);
        // 200 MB total private - 90 MB measured Windows non-GC overhead = 110 MB.
        Assert.True(Math.Max(loadCommitted, refreshLoadCommitted) < 110_000_000,
            $"Committed after load {loadCommitted:N0}, after refresh load {refreshLoadCommitted:N0}; ceiling < 110 MB.");
        Assert.True(Math.Max(itemCommitted, refreshCommitted) < 110_000_000,
            $"Natural GC committed: item {itemCommitted:N0}, refresh {refreshCommitted:N0}; ceiling < 110 MB (200 - 90).");
        Assert.True(final - initial < 50_000_000,
            $"Pack live heap delta {final - initial:N0}; tray delta {tray - initial:N0}; target < 50 MB.");
        Assert.True(firstUse.Elapsed.TotalMilliseconds < 500, $"First item use took {firstUse.Elapsed.TotalMilliseconds:F1} ms; target < 500 ms.");
    }
}
