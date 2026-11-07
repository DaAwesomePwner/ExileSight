using ExileSight.App.Features.SearchStrings;
using ExileSight.Core.Features;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.SearchStrings.Tests;

public sealed class SelectionTests
{
    [Theory]
    [InlineData(1499, 1)]
    [InlineData(1500, 0)]
    [InlineData(1501, 0)]
    public void PreparedSelectionExpiresAtOneAndAHalfSeconds(int elapsed, int expected)
    {
        var clock = new ManualTime();
        var feature = new SearchStringsFeature(clock);
        var calls = 0;
        var host = Host(feature, _ => { calls++; return new(ActionOutcome.Success); });
        feature.Menu.ChooseVendor("Vendor"); feature.Menu.Prepare(0);
        clock.Milliseconds = elapsed;
        Paste(host).Execute(new IntentToken());
        Paste(host).Execute(new IntentToken());
        Assert.Equal(expected, calls);
    }
    [Theory]
    [InlineData(ActionOutcome.Success)]
    [InlineData(ActionOutcome.MissingToken)]
    [InlineData(ActionOutcome.UsedOrUnknownToken)]
    [InlineData(ActionOutcome.Expired)]
    [InlineData(ActionOutcome.NotForeground)]
    [InlineData(ActionOutcome.Undeclared)]
    [InlineData(ActionOutcome.WrongAction)]
    [InlineData(ActionOutcome.Failed)]
    public async Task EveryGateOutcomeClearsSelectionAndCloses(ActionOutcome outcome)
    {
        var feature = new SearchStringsFeature();
        var calls = 0; var closes = 0;
        var host = Host(feature, _ => { calls++; return new(outcome); });
        feature.Menu.ChooseVendor("Vendor");
        await SearchClick.RunAsync(() => feature.Menu.Prepare(0), () => new IntentToken(),
            token => Task.FromResult(Paste(host).Execute(token)), feature.Menu.Cancel, () => closes++,
            _ => Assert.Fail("Unexpected exception"));
        Paste(host).Execute(new IntentToken());
        Assert.Equal(1, calls); Assert.Equal(1, closes);
    }
    [Fact]
    public async Task MissingRedemptionClearsSelectionBeforeLaterHotkey()
    {
        var feature = new SearchStringsFeature();
        var calls = 0; var closes = 0;
        var host = Host(feature, _ => { calls++; return new(ActionOutcome.Success); });
        feature.Menu.ChooseVendor("Vendor");
        await SearchClick.RunAsync(() => feature.Menu.Prepare(0), () => null,
            _ => throw new InvalidOperationException("No token must not dispatch"), feature.Menu.Cancel,
            () => closes++, _ => Assert.Fail("Unexpected exception"));
        Paste(host).Execute(new IntentToken());
        Assert.Equal(0, calls); Assert.Equal(1, closes);
    }
    [Fact]
    public async Task PrepareFailureIsGuardedAndClearsPreviousSelection()
    {
        var feature = new SearchStringsFeature();
        var calls = 0; var closes = 0;
        var errors = new List<Exception>();
        var host = Host(feature, _ => { calls++; return new(ActionOutcome.Success); });
        feature.Menu.ChooseVendor("Vendor"); feature.Menu.Prepare(0);
        await SearchClick.RunAsync(() => feature.Menu.Prepare(999),
            () => throw new InvalidOperationException("Must not redeem after failed preparation"),
            _ => throw new InvalidOperationException("Must not dispatch"), feature.Menu.Cancel,
            () => closes++, errors.Add);
        Paste(host).Execute(new IntentToken());
        Assert.Equal(0, calls); Assert.Equal(1, closes);
        Assert.IsType<IndexOutOfRangeException>(Assert.Single(errors));
    }
    [Fact]
    public async Task HiddenMenuCancelsSelectionBeforeQueuedExecution()
    {
        var feature = new SearchStringsFeature();
        var calls = 0;
        var host = Host(feature, _ => { calls++; return new(ActionOutcome.Success); });
        feature.Menu.ChooseVendor("Vendor");
        await SearchClick.RunAsync(() => feature.Menu.Prepare(0), () => new IntentToken(), token =>
        {
            feature.Menu.Cancel(); // IsVisibleChanged unconditionally calls Cancel, even during submission.
            return Task.FromResult(Paste(host).Execute(token));
        }, feature.Menu.Cancel, () => { }, _ => Assert.Fail("Unexpected exception"));
        Assert.Equal(0, calls);
    }
    [Fact]
    public async Task DispatchExceptionStillClearsAndCloses()
    {
        var feature = new SearchStringsFeature();
        var calls = 0; var closes = 0;
        var errors = new List<Exception>();
        var host = Host(feature, _ => { calls++; return new(ActionOutcome.Success); });
        feature.Menu.ChooseVendor("Vendor");
        await SearchClick.RunAsync(() => feature.Menu.Prepare(0), () => new IntentToken(),
            _ => Task.FromException<ActionResult>(new InvalidOperationException()), feature.Menu.Cancel,
            () => closes++, errors.Add);
        Paste(host).Execute(new IntentToken());
        Assert.Equal(0, calls); Assert.Equal(1, closes); Assert.Single(errors);
    }
    private static FeatureHost Host(SearchStringsFeature feature, Func<GameAction, ActionResult> execute) =>
        new([feature], (_, _) => null, (_, _, _) => { }, (_, _, action) => execute(action));
    private static IFeatureCommandHandler Paste(FeatureHost host) => host.Commands.Single(c => c.Command == "searchstr:paste");
    private sealed class ManualTime : TimeProvider
    {
        public long Milliseconds { get; set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }
}
