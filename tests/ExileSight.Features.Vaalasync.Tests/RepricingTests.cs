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
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new ShopModel(new(directory), () => "Standard");
        var calls = 0;
        var host = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { }, (_, _, _) =>
        {
            Interlocked.Increment(ref calls);
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            return new(ActionOutcome.Success, PasteStatus: "");
        });
        host.Route(Item, new(Game.PoE1), OmniPressKind.Short);
        var send = Task.Run(() => model.Apply("plus1", new()));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            await Task.Run(() =>
            {
                Assert.NotNull(model.Note);
                Assert.Contains("Setting note", model.Status);
                Assert.False(model.CanApply);
                model.Custom = "1/3";
                Assert.Equal("1/3", model.Custom);
                Assert.Equal(ActionOutcome.Failed, model.Apply("plus1", new()).Outcome);
                host.Route(Item, new(Game.PoE1), OmniPressKind.Short);
                Assert.False(model.CanApply);
            }).WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally { release.Set(); await send.WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.Equal(1, calls);
        Assert.False(model.CanApply);
        Assert.Equal(ActionOutcome.Failed, model.Apply("plus1", new()).Outcome);
        Assert.Equal("Sent", model.History.Snapshot()[0].Prices.Last().Kind);
        host.Route(Item, new(Game.PoE1), OmniPressKind.Short);
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
