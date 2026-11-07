using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Features.Vaalasync;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class SendOutcomeTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "vaalasync-outcomes-" + Guid.NewGuid());
    private int calls;
    private ShopModel Create(Func<ActionResult> execute)
    {
        var model = new ShopModel(new(directory), () => "Standard");
        var host = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { },
            (_, _, _) => { calls++; return execute(); });
        var item = ItemTextParser.Parse("Item Class: Rings\nRarity: Normal\nIron Ring\n--------\nNote: ~price 2 divine", Game.PoE1).Item!;
        host.Route(item, new(Game.PoE1), OmniPressKind.Short);
        return model;
    }
    [Theory]
    [InlineData("Clipboard restore failed; action text cleared", "Sent (paste confirmed)")]
    [InlineData("Partial send", "Not sent completely")]
    [InlineData("Clipboard rendering failed after send", "Not applicable")]
    public void AmbiguousFailureDisarmsAndPersistsPossiblePrice(string reason, string paste)
    {
        var model = Create(() => new(ActionOutcome.Failed, reason, PasteStatus: paste));
        Assert.Equal(ActionOutcome.Failed, model.Apply("plus1", new()).Outcome);
        Assert.False(model.CanApply);
        Assert.Contains("press omni again", model.Status);
        Assert.Contains(reason, model.Status);
        var entry = new HistoryStore(directory).Snapshot()[0].Prices.Last();
        Assert.StartsWith("Possibly set", entry.Kind);
        Assert.Equal("~price 3 divine", entry.Note.ToString());
        Assert.Equal(ActionOutcome.Failed, model.Apply("plus1", new()).Outcome);
        Assert.Equal(1, calls);
    }
    [Theory]
    [InlineData(ActionOutcome.MissingToken)]
    [InlineData(ActionOutcome.UsedOrUnknownToken)]
    [InlineData(ActionOutcome.Expired)]
    [InlineData(ActionOutcome.NotForeground)]
    [InlineData(ActionOutcome.Undeclared)]
    [InlineData(ActionOutcome.WrongAction)]
    public void ExplicitPreInputRefusalsKeepArmedWithoutPriceHistory(ActionOutcome outcome)
    {
        var model = Create(() => new(outcome));
        Assert.Equal(outcome, model.Apply("plus1", new()).Outcome);
        Assert.True(model.CanApply);
        Assert.Single(model.History.Snapshot()[0].Prices);
        model.Apply("plus1", new());
        Assert.Equal(2, calls);
    }
    [Fact]
    public void ExecutorExceptionDisarmsAndRecordsUncertainty()
    {
        var model = Create(() => throw new IOException("Could throw after inserting input"));
        var errors = new List<string>();
        model.ReportFailure = errors.Add;
        Assert.Equal(ActionOutcome.Failed, model.Apply("plus1", new()).Outcome);
        Assert.False(model.CanApply);
        Assert.Contains("press omni again", model.Status);
        Assert.Equal("Possibly set", new HistoryStore(directory).Snapshot()[0].Prices.Last().Kind);
        Assert.Equal(["IOException"], errors);
        model.Apply("plus1", new());
        Assert.Equal(1, calls);
    }
    [Theory]
    [InlineData("Sent (paste confirmed)")]
    [InlineData("Sent (unconfirmed)")]
    public void SuccessDisplaysAndStoresPasteOutcomeOnce(string paste)
    {
        var model = Create(() => new(ActionOutcome.Success, paste, PasteStatus: paste));
        Assert.Equal(ActionOutcome.Success, model.Apply("plus1", new()).Outcome);
        Assert.Equal(ShopModel.RearmMessage + " — " + paste, model.Status);
        Assert.Equal(paste, new HistoryStore(directory).Snapshot()[0].Prices.Last().Kind);
        Assert.False(model.CanApply);
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
