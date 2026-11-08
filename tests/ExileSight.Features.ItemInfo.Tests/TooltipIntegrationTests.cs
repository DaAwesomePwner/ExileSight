using ExileSight.App.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Items;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class TooltipIntegrationTests
{
    [Fact]
    public async Task StartupReadsOnlyLocalPacksAndPublishesPreparedData()
    {
        var reads = new List<string>();
        var refreshes = new List<string>();
        var startup = new PackStartup((id, _) =>
        {
            reads.Add(id);
            return Task.FromResult<(Core.DataPacks.DataPack?, string)>((null, "none"));
        }, (id, _) => { refreshes.Add(id); return Task.CompletedTask; });
        Assert.Null(startup.Data);
        Assert.Equal("Packs loading", startup.Status);
        await startup.LoadAsync("Not checked", default);
        Assert.Equal(PackStartup.PackIds, reads);
        Assert.Empty(refreshes);
        Assert.NotNull(startup.Data);
        Assert.Contains("unavailable", startup.Status);
        Assert.All(startup.Snapshot().Take(3), row => Assert.Contains("(none)", row.Value));
        var prepared = startup.Data;
        await startup.RefreshAsync(default);
        Assert.Same(prepared, startup.Data);
        Assert.Equal("Packs unavailable", startup.Status);
        Assert.Equal(PackStartup.PackIds, refreshes);
    }
    [Fact]
    public async Task CancelledPreparationDoesNotPublishIncompleteReplacement()
    {
        using var cancellation = new CancellationTokenSource();
        var startup = new PackStartup((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<(Core.DataPacks.DataPack?, string)>((null, "none"));
        }, (_, _) => Task.CompletedTask);
        await startup.LoadAsync("Not checked", default);
        var original = startup.Data;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startup.LoadAsync("cancelled", cancellation.Token));
        Assert.Same(original, startup.Data);
    }
    [Fact]
    public void RegisteredHandlerRendersMainSectionAndLaterContributors()
    {
        var reads = 0;
        var feature = new ItemInfoFeature { ReadData = () => { reads++; return null; } };
        var host = new FeatureHost([feature, new ExtraFeature()], (_, _) => null, (_, _, _) => { });
        var item = ItemTextParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures/hybrid.txt")), Game.PoE1).Item!;
        Assert.True(host.Route(item, new(Game.PoE1), OmniPressKind.Short));
        Assert.Equal(1, reads);
        Assert.Equal(["iteminfo", "extra"], feature.ViewModel.Sections.Select(s => s.Id));
        var text = string.Join(" ", feature.ViewModel.Sections[0].Rows.SelectMany(r => r.Text).Select(t => t.Text));
        Assert.Contains("ilvl unknown", text);
        Assert.DoesNotContain("10000", text);
        feature.ViewModel.Close();
        Assert.Null(feature.ViewModel.Current);
    }
    [Fact]
    public async Task OnePackFailureDoesNotSkipOtherRefreshesOrLocalReload()
    {
        var refreshes = new List<string>();
        var reads = new List<string>();
        var logged = new List<(string Id, string Type)>();
        var startup = new PackStartup((id, _) =>
        {
            reads.Add(id);
            return Task.FromResult<(Core.DataPacks.DataPack?, string)>((null, "none"));
        }, (id, _) =>
        {
            refreshes.Add(id);
            if (id == "poe1/bases") throw new IOException("sensitive path must not appear in diagnostics");
            return Task.CompletedTask;
        }, (id, error) => logged.Add((id, error.GetType().Name)));
        await startup.RefreshAsync(default);
        Assert.Equal(PackStartup.PackIds, refreshes);
        Assert.Equal(PackStartup.PackIds, reads);
        Assert.Equal(("poe1/bases", "IOException"), Assert.Single(logged));
        Assert.NotNull(startup.Data);
        var diagnostic = startup.Snapshot().Single(row => row.Key == "Last refresh").Value;
        Assert.Contains("poe1/bases: IOException", diagnostic);
        Assert.DoesNotContain("sensitive", diagnostic);
    }
    private sealed class ExtraFeature : IFeature, ITooltipSectionProvider
    {
        public FeatureManifest Manifest { get; } = new("extra", [Game.PoE1], [], [], [], "test");
        public int Priority => 200;
        public void Register(IFeatureContext context) => context.RegisterTooltipSection(this);
        public TooltipSection Build(ParsedItem item) => new("extra", "Comparison", []);
    }
}
