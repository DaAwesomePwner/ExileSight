using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class PressRouterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpiredPressReportsOutcomeWithoutExecuting(bool feature)
    {
        var clock = new TestClock();
        var registry = new IntentRegistry(clock);
        var dispatcher = new HotkeyDispatcher(registry, []);
        var press = Press(clock.Milliseconds, feature);
        var calls = 0;
        var router = new PressRouter(dispatcher, clock, () => true, (_, _) =>
        { calls++; return new(ActionOutcome.Success); });
        clock.Delay(1500);
        Assert.Equal(ActionOutcome.Expired, router.Route(press).Outcome);
        Assert.Equal(0, calls);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, router.Route(press).Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GameContextRechecksForegroundBeforeBothRoutes(bool feature)
    {
        var clock = new TestClock();
        var dispatcher = new HotkeyDispatcher(new(clock), []);
        var calls = 0;
        var focused = true;
        var press = Press(clock.Milliseconds, feature);
        var router = new PressRouter(dispatcher, clock, () => focused, (_, _) =>
        { calls++; return new(ActionOutcome.Success); });
        focused = false; // focus changed after the hook matched the press
        Assert.Equal(ActionOutcome.NotForeground, router.Route(press).Outcome);
        Assert.Equal(0, calls);
        focused = true;
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, router.Route(press).Outcome);
    }

    [Fact]
    public void GameActionReceivesSingleUseMatchingToken()
    {
        var clock = new TestClock();
        var registry = new IntentRegistry(clock);
        var dispatcher = new HotkeyDispatcher(registry, []);
        var policy = new GatePolicy(registry, [ActionKind.CopyItem]);
        IntentToken? issued = null;
        var router = new PressRouter(dispatcher, clock, () => true, (token, binding) =>
        {
            issued = token;
            return new(policy.Validate(token, binding.Action!, true, out _));
        });
        var press = Press(clock.Milliseconds, false);
        Assert.Equal(ActionOutcome.Success, router.Route(press).Outcome);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, router.Route(press).Outcome);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, registry.Consume(issued, out _));
    }

    [Theory]
    [InlineData("gameFocused", true)]
    [InlineData("overlay", false)]
    public void LocalFeatureExecutesOnceWithoutGameAuthority(string context, bool focused)
    {
        var clock = new TestClock();
        var dispatcher = new HotkeyDispatcher(new(clock), []);
        var command = new FakeCommand();
        var execution = new HotkeyExecution(null!, [command]);
        var router = new PressRouter(dispatcher, clock, () => focused, (token, binding) =>
        {
            Assert.Null(token);
            return execution.Execute(token, binding);
        });
        var press = new PhysicalPress(clock.Milliseconds, new("feature", 118, 0, false, null, context, command.Command));
        Assert.Equal(ActionOutcome.Success, router.Route(press).Outcome);
        Assert.Equal(1, command.Calls);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, router.Route(press).Outcome);
    }

    private static PhysicalPress Press(long timestamp, bool feature) =>
        new(timestamp, new("test", 118, 0, true, feature ? null : new GameAction.CopyItem(),
            FeatureCommand: feature ? "toggle" : null));
    private sealed class FakeCommand : IFeatureCommandHandler
    {
        public string Command => "toggle";
        public int Calls { get; private set; }
        public ActionResult Execute() { Calls++; return new(ActionOutcome.Success); }
    }
}
