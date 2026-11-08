using System.Collections.Concurrent;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Core.Pricing;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class HistoryShutdownTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "shop-shutdown-" + Guid.NewGuid());
    private static ParsedItem Item(string note = "") => ItemTextParser.Parse(
        "Item Class: Rings\nRarity: Normal\nIron Ring\n--------\n" + note, Game.PoE1).Item!;
    private static void Register(ShopModel model) =>
        _ = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { },
            (_, _, _) => new(ActionOutcome.Success));

    [Theory]
    [InlineData(null, false, "Standard")]
    [InlineData(null, true, "Example Challenge")]
    [InlineData("Hardcore", true, "Hardcore")]
    public async Task CancelledDiscoveryPreservesRealSendWithBestKnownLeague(string? configured, bool cached, string expected)
    {
        string? setting = null;
        var pending = new TaskCompletionSource<LeagueResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var discovery = new ShopLeague(() => setting, () => cached
            ? Task.FromResult(new LeagueResult(["Example Challenge"], "Example Challenge", false, null)) : pending.Task);
        if (cached) await discovery.ResolveAsync();
        cached = false;
        var reports = new ConcurrentQueue<string>();
        var model = new ShopModel(new(directory), () => discovery.Current)
        {
            ResolveLeagueAsync = discovery.ResolveAsync,
            ReportFailure = reports.Enqueue
        };
        Register(model);
        model.Handle(Item("Note: ~b/o 12 chaos"), new(Game.PoE1), OmniPressKind.Short);
        Assert.Equal(ActionOutcome.Success, model.Apply("plus1", new()).Outcome);
        Assert.Empty(model.History.Snapshot());
        setting = configured;
        pending.SetCanceled();
        await model.StopAndFlushHistoryAsync(TimeSpan.FromSeconds(5));
        var row = Assert.Single(new HistoryStore(directory).Snapshot());
        Assert.Equal(expected, row.League);
        Assert.Equal(new decimal[] { 12, 13 }, row.Prices.Select(p => p.Note.Amount));
        Assert.Contains(reports, text => text.Contains("League unconfirmed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShutdownDrainsWithoutWaitingForDiscoveryAndRejectsNewInput()
    {
        var pending = new TaskCompletionSource<string>();
        var reports = new ConcurrentQueue<string>();
        var model = new ShopModel(new(directory), () => "Hardcore")
        {
            ResolveLeagueAsync = () => pending.Task,
            ReportFailure = reports.Enqueue
        };
        Register(model);
        model.Handle(Item(), new(Game.PoE1), OmniPressKind.Long);
        model.Custom = "17";
        Assert.Equal(ActionOutcome.Success, model.Apply("custom", new()).Outcome);
        await model.StopAndFlushHistoryAsync(TimeSpan.FromSeconds(5));
        Assert.False(pending.Task.IsCompleted);
        var row = Assert.Single(new HistoryStore(directory).Snapshot());
        Assert.Equal("Hardcore", row.League);
        Assert.Equal(17, Assert.Single(row.Prices).Note.Amount);
        Assert.Contains(reports, text => text.Contains("League unconfirmed", StringComparison.Ordinal));
        model.Handle(Item(), new(Game.PoE1), OmniPressKind.Long);
        Assert.Equal(ActionOutcome.Failed, model.Apply("custom", new()).Outcome);
    }

    [Fact]
    public async Task FailedDiscoveryAlsoRecordsAndReportsFallback()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reports = new ConcurrentQueue<string>();
        var model = new ShopModel(new(directory), () => "Standard")
        { ResolveLeagueAsync = () => pending.Task, ReportFailure = reports.Enqueue };
        Register(model);
        model.Handle(Item("Note: ~b/o 12 chaos"), new(Game.PoE1), OmniPressKind.Short);
        model.Apply("plus1", new());
        pending.SetException(new IOException());
        await model.StopAndFlushHistoryAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, Assert.Single(new HistoryStore(directory).Snapshot()).Prices.Length);
        Assert.Contains(reports, text => text.Contains("League unconfirmed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OlderSendFailureDoesNotAppendToNewReadStatus()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reports = new ConcurrentQueue<string>();
        var model = new ShopModel(new(directory), () => "Standard")
        { ResolveLeagueAsync = () => pending.Task, ReportFailure = reports.Enqueue };
        Register(model);
        model.Handle(Item(), new(Game.PoE1), OmniPressKind.Long);
        model.Custom = "17";
        model.Apply("custom", new());
        model.Handle(Item(), new(Game.PoE1), OmniPressKind.Long);
        var newerStatus = model.Status;
        Directory.CreateDirectory(Path.Combine(directory, "history.json")); // Force only the queued send's write to fail.
        pending.SetResult("Standard");
        await model.StopAndFlushHistoryAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(newerStatus, model.Status);
        Assert.NotEmpty(reports);
        Assert.Empty(model.History.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownWaitsForActiveSendUnlessHostBudgetExpires(bool cancelHost)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var pending = new TaskCompletionSource<string>();
        var reports = new ConcurrentQueue<string>();
        using var hostStop = new CancellationTokenSource();
        var model = new ShopModel(new(directory), () => "Standard")
        { ResolveLeagueAsync = () => pending.Task, ReportFailure = reports.Enqueue };
        _ = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { },
            (_, _, _) =>
            {
                entered.SetResult();
                release.Wait();
                return new(ActionOutcome.Success);
            });
        model.Handle(Item("Note: ~b/o 12 chaos"), new(Game.PoE1), OmniPressKind.Short);
        var apply = Task.Run(() => model.Apply("plus1", new()));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stop = model.StopAndFlushHistoryAsync(TimeSpan.FromSeconds(5), hostStop.Token);
            Assert.False(stop.IsCompleted);
            if (cancelHost)
            {
                hostStop.Cancel();
                await stop.WaitAsync(TimeSpan.FromSeconds(1));
                Assert.False(apply.IsCompleted);
                Assert.Contains(reports, text => text.Contains("not saved before shutdown", StringComparison.Ordinal));
            }
            release.Set();
            await stop;
            Assert.Equal(ActionOutcome.Success, (await apply).Outcome);
            await model.StopAndFlushHistoryAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, Assert.Single(new HistoryStore(directory).Snapshot()).Prices.Length);
        }
        finally { release.Set(); await apply; }
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
