using System.Diagnostics;
using System.Text.Json;
using ExileSight.App.DataPacks;
using ExileSight.Core.DataPacks.Loading;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.ItemInfo.Tests;

[Collection("Real pack probes")]
public sealed class CardLatencyTests(ITestOutputHelper output)
{
    [RealPacksFact]
    public async Task MeasureFirstItemPresentationWithShippingPacks()
    {
        using var transport = new GitHubTransport("card-latency-probe");
        var loader = new PackLoader(Environment.GetEnvironmentVariable("EXILESIGHT_REAL_PACKS")!, transport);
        var startup = new PackStartup(async (id, token) =>
        {
            var loaded = await loader.ReadLocalAsync(id, token);
            Assert.NotNull(loaded.Pack);
            return (loaded.Pack, "fixture");
        }, (_, _) => Task.CompletedTask);
        await startup.LoadAsync("probe", default);
        var item = FirstUseFixture.Parse();
        var card = Environment.GetEnvironmentVariable("EXILESIGHT_CARD_LATENCY_MODE") == "card";
        // Use a fresh test process per mode. No matcher or calculator warm-up.
        var watch = Stopwatch.StartNew();
        var result = card ? new ItemInfoCalculator(startup.Data).Calculate(item)
            : new BaselineCalculator(startup.Data).Calculate(item);
        object presentation = card ? ItemCardBuilder.Build(result, startup.Data) : BaselineTextSection.Render(result);
        watch.Stop();
        var report = JsonSerializer.Serialize(new
        {
            mode = card ? "card" : "text", milliseconds = watch.Elapsed.TotalMilliseconds,
            scope = "First calculator plus presentation model after production pack startup; excludes WPF",
            runtime = Environment.Version.ToString(), os = Environment.OSVersion.ToString()
        });
        output.WriteLine(report);
        if (Environment.GetEnvironmentVariable("EXILESIGHT_CARD_LATENCY_REPORT") is { } path)
            await File.WriteAllTextAsync(path, report);
        GC.KeepAlive(presentation);
        GC.KeepAlive(startup);
        Assert.True(watch.Elapsed.TotalMilliseconds < 500, report);
    }
}
