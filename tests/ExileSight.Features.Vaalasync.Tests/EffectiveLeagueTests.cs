using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Core.Pricing;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class EffectiveLeagueTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "shop-league-" + Guid.NewGuid());
    private static LeagueResult Current => new(["Standard", "Example Challenge"], "Example Challenge", false, null);
    private static ParsedItem Item(string note = "") => ItemTextParser.Parse(
        "Item Class: Rings\nRarity: Normal\nIron Ring\n--------\n" + note, Game.PoE1).Item!;

    [Theory]
    [InlineData(null, false, "Example Challenge")]
    [InlineData("Hardcore", false, "Hardcore")]
    [InlineData(null, true, "Example Challenge")]
    public void EstimatesAndNewListingsShareResolvedLeague(string? configured, bool stale, string expected)
    {
        var league = new ShopLeague(() => configured, () => Task.FromResult(Current with { IsStale = stale }));
        var requested = new List<string>();
        var model = new ShopModel(new(directory), () => league.Current)
        {
            ResolveLeagueAsync = league.ResolveAsync,
            SeedPrice = (item, resolved) => ListingPrices.FindAsync((name, _) =>
            {
                requested.Add(name);
                return Task.FromResult(new PriceResult(new([], null), null, false, null));
            }, resolved, item)
        };
        _ = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { },
            (_, _, _) => new(ActionOutcome.Success));
        model.Handle(Item(), new(Game.PoE1), OmniPressKind.Long);
        Assert.NotEmpty(requested);
        Assert.All(requested, value => Assert.Equal(expected, value));
        model.Custom = "12";
        model.Apply("custom", new());
        var row = Assert.Single(model.History.Snapshot());
        Assert.Equal(expected, row.League);
        model.History.Evidence(new("", null, "@From", "", "", row.Item, "12 chaos", expected, ""), DateTimeOffset.UtcNow);
        Assert.NotNull(Assert.Single(model.History.Snapshot()).Evidence);
    }

    [Fact]
    public async Task PendingDiscoveryDefersHistoryButArmsAndSendsImmediately()
    {
        var pending = new TaskCompletionSource<LeagueResult>();
        var ready = new TaskCompletionSource();
        var league = new ShopLeague(() => null, () => pending.Task);
        var model = new ShopModel(new(directory), () => league.Current) { ResolveLeagueAsync = league.ResolveAsync };
        model.Changed += () => { if (model.History.Snapshot().Any(r => r.Prices.Length == 2)) ready.TrySetResult(); };
        model.Handle(Item("Note: ~b/o 12 chaos"), new(Game.PoE1), OmniPressKind.Short);
        var sends = 0;
        _ = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { },
            (_, _, _) => { sends++; return new(ActionOutcome.Success); });
        Assert.True(model.CanApply);
        Assert.Empty(model.History.Snapshot());
        Assert.Equal(ActionOutcome.Success, model.Apply("plus1", new()).Outcome);
        Assert.Equal(1, sends);
        Assert.False(model.CanApply);
        Assert.Empty(model.History.Snapshot());
        pending.SetResult(Current);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var row = Assert.Single(model.History.Snapshot());
        Assert.Equal("Example Challenge", row.League);
        Assert.Equal(new decimal[] { 12, 13 }, row.Prices.Select(p => p.Note.Amount));
        Assert.False(model.CanApply);
    }

    [Fact]
    public async Task ConfigurationWinsEvenWhenChangedDuringDiscovery()
    {
        string? configured = null;
        var pending = new TaskCompletionSource<LeagueResult>();
        var league = new ShopLeague(() => configured, () => pending.Task);
        var resolving = league.ResolveAsync();
        configured = "Hardcore";
        Assert.Equal("Hardcore", league.Current);
        pending.SetResult(Current);
        Assert.Equal("Hardcore", await resolving);
        var direct = new ShopLeague(() => configured, () => throw new InvalidOperationException("Must not discover"));
        Assert.Equal("Hardcore", await direct.ResolveAsync());
    }

    [Fact]
    public async Task StaleDiscoveryRetainsCachedDefaultAndMissingDataUsesStandard()
    {
        var result = Current;
        var offline = false;
        var league = new ShopLeague(() => null, () => offline
            ? Task.FromException<LeagueResult>(new IOException()) : Task.FromResult(result));
        Assert.Equal("Example Challenge", await league.ResolveAsync());
        result = Current with { IsStale = true };
        Assert.Equal("Example Challenge", await league.ResolveAsync());
        offline = true;
        Assert.Equal("Example Challenge", await league.ResolveAsync());
        offline = false;
        result = new([], "Standard", true, "Offline");
        Assert.Equal("Standard", await league.ResolveAsync());
        offline = true;
        Assert.Equal("Standard", await league.ResolveAsync());
    }

    [Fact]
    public async Task OverlappingReadsShareDiscoveryAndPreservePendingSend()
    {
        var discovery = new TaskCompletionSource<LeagueResult>();
        var calls = 0;
        var ready = new TaskCompletionSource();
        var league = new ShopLeague(() => null, () => { calls++; return discovery.Task; });
        var model = new ShopModel(new(directory), () => league.Current) { ResolveLeagueAsync = league.ResolveAsync };
        _ = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { },
            (_, _, _) => new(ActionOutcome.Success));
        model.Changed += () =>
        {
            if (model.History.Snapshot().SingleOrDefault()?.Prices.Length == 3) ready.TrySetResult();
        };
        model.Handle(Item("Note: ~b/o 1 chaos"), new(Game.PoE1), OmniPressKind.Short);
        model.Apply("plus1", new());
        model.Handle(Item("Note: ~b/o 3 chaos"), new(Game.PoE1), OmniPressKind.Short);
        Assert.True(model.CanApply);
        Assert.Equal(1, calls);
        discovery.SetResult(Current);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var row = Assert.Single(model.History.Snapshot());
        Assert.Equal("Example Challenge", row.League);
        Assert.Equal(new decimal[] { 1, 2, 3 }, row.Prices.Select(p => p.Note.Amount));
    }

    [Fact]
    public async Task UnpricedSendBeforeDiscoveryRecordsOnlyTheSendAndDoesNotRearm()
    {
        var pending = new TaskCompletionSource<string>();
        var saved = new TaskCompletionSource();
        var model = new ShopModel(new(directory), () => "Standard")
        {
            ResolveLeagueAsync = () => pending.Task,
            SeedPrice = (_, _) => Task.FromResult<ShopNote?>(new("~b/o", 99, "chaos"))
        };
        _ = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { },
            (_, _, _) => new(ActionOutcome.Success));
        model.Changed += () => { if (model.History.Snapshot().Length > 0) saved.TrySetResult(); };
        model.Handle(Item(), new(Game.PoE1), OmniPressKind.Long);
        model.Custom = "17";
        Assert.Equal(ActionOutcome.Success, model.Apply("custom", new()).Outcome);
        Assert.Empty(model.History.Snapshot());
        pending.SetResult("Example Challenge");
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var row = Assert.Single(model.History.Snapshot());
        Assert.Equal("Example Challenge", row.League);
        Assert.Equal(17, Assert.Single(row.Prices).Note.Amount);
        Assert.Equal("17", model.Custom);
        Assert.False(model.CanApply);
    }

    [Fact]
    public void ThrowingSubscriberDoesNotChangeHistoryOrPreventInput()
    {
        var failures = new List<string>();
        var model = new ShopModel(new(directory), () => "Standard") { ReportFailure = failures.Add };
        _ = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { },
            (_, _, _) => new(ActionOutcome.Success));
        model.Changed += () => throw new InvalidOperationException();
        model.Handle(Item("Note: ~b/o 1 chaos"), new(Game.PoE1), OmniPressKind.Short);
        Assert.True(model.CanApply);
        Assert.DoesNotContain("unavailable", model.Status);
        Assert.Equal(ActionOutcome.Success, model.Apply("plus1", new()).Outcome);
        Assert.Equal(2, Assert.Single(model.History.Snapshot()).Prices.Length);
        Assert.NotEmpty(failures);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
