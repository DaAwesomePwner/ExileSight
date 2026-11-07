using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Features.Vaalasync;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class RepricingTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "vaalasync-reprice-" + Guid.NewGuid());
    private static ParsedItem Item => ItemTextParser.Parse(
        "Item Class: Rings\nRarity: Normal\nIron Ring\n--------\nNote: ~price 2 divine", Game.PoE1).Item!;
    [Fact]
    public async Task ReadsStayResponsiveDuringSendAndConcurrentClickCannotSendAgain()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var model = new ShopModel(new(directory), () => "Standard");
        var calls = 0;
        var queue = new FeatureWorkQueue();
        var host = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { }, (_, _, _) =>
        {
            Interlocked.Increment(ref calls);
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(30)));
            return new(ActionOutcome.Success, PasteStatus: "");
        }, dispatch: queue.Post);
        async Task RouteAsync()
        {
            var routed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Post(() =>
            {
                try
                {
                    Assert.True(host.Route(Item, new(Game.PoE1), OmniPressKind.Short));
                    routed.SetResult();
                }
                catch (Exception error) { routed.SetException(error); }
            });
            await routed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        await RouteAsync();
        // Blocking executor work must never occupy a thread-pool worker.
        var send = Task.Factory.StartNew(() => model.Apply("plus1", new()),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task? reads = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            reads = Task.Factory.StartNew(async () =>
            {
                Assert.NotNull(model.Note);
                Assert.Contains("Setting note", model.Status);
                Assert.False(model.CanApply);
                model.Custom = "1/3";
                Assert.Equal("1/3", model.Custom);
                Assert.Equal(ActionOutcome.Failed, model.Apply("plus1", new()).Outcome);
                await RouteAsync();
                Assert.False(model.CanApply);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
            await reads.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.False(send.IsCompleted);
        }
        finally
        {
            release.Set();
            await send.WaitAsync(TimeSpan.FromSeconds(30));
            if (reads is not null) await reads.WaitAsync(TimeSpan.FromSeconds(30));
        }
        Assert.Equal(ActionOutcome.Success, (await send).Outcome);
        Assert.Equal(1, calls);
        Assert.False(model.CanApply);
        Assert.Equal(ActionOutcome.Failed, model.Apply("plus1", new()).Outcome);
        Assert.Equal("Sent", model.History.Snapshot()[0].Prices.Last().Kind);
        await RouteAsync();
        Assert.True(model.CanApply);
    }
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, false)]
    public void SystemClassifiedDoubleClicksAreIgnored(int systemClickCount, bool accepted) =>
        Assert.Equal(accepted, ShopModel.AcceptClick(systemClickCount));
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
