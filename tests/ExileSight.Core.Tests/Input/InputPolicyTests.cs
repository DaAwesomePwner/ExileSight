using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class FakeClock : IClock
{
    public long Milliseconds { get; set; } = 10000;
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch.AddMilliseconds(Milliseconds);
}
public sealed class InputPolicyTests
{
    private readonly FakeClock clock = new();
    private static readonly GameAction Copy = new GameAction.CopyItem();
    private IntentToken Token(IntentRegistry registry, long? timestamp = null)
    {
        var dispatcher = new HotkeyDispatcher(registry, [new("copy", 118, 0, true, Copy)]);
        var decision = dispatcher.Observe(new(118, true, false, 0, timestamp ?? clock.Milliseconds), true);
        return dispatcher.Issue(decision.Press!);
    }
    [Fact]
    public void MissingAndForgedTokensAreRefused()
    {
        var registry = new IntentRegistry(clock);
        Assert.Equal(ActionOutcome.MissingToken, registry.Consume(null, out _));
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, registry.Consume(new IntentToken(), out _));
    }
    [Theory]
    [InlineData(1499, ActionOutcome.Success)]
    [InlineData(1500, ActionOutcome.Expired)]
    [InlineData(1501, ActionOutcome.Expired)]
    [InlineData(-1, ActionOutcome.Expired)]
    public void LifetimeUsesPhysicalTimestamp(long age, ActionOutcome expected)
    {
        var registry = new IntentRegistry(clock);
        var token = Token(registry);
        clock.Milliseconds += age;
        Assert.Equal(expected, registry.Consume(token, out var press));
        Assert.Equal("copy", press!.HotkeyId);
        Assert.Equal(10000, press.Timestamp);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, registry.Consume(token, out _));
    }
    [Theory]
    [InlineData(false, true, ActionOutcome.NotForeground)]
    [InlineData(true, false, ActionOutcome.Undeclared)]
    [InlineData(true, true, ActionOutcome.Success)]
    public void PolicyConsumesEvenOnFailure(bool foreground, bool declared, ActionOutcome expected)
    {
        var registry = new IntentRegistry(clock);
        var gate = new GatePolicy(registry, declared ? [ActionKind.CopyItem] : []);
        var token = Token(registry);
        Assert.Equal(expected, gate.Validate(token, Copy, foreground, out _));
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, gate.Validate(token, Copy, true, out _));
    }
    [Fact]
    public void QueuingDoesNotExtendLifetime()
    {
        var registry = new IntentRegistry(clock);
        var token = Token(registry, clock.Milliseconds - 1600);
        Assert.Equal(ActionOutcome.Expired, registry.Consume(token, out _));
    }
    [Fact]
    public void OnlyOneConcurrentConsumerSucceeds()
    {
        var registry = new IntentRegistry(clock);
        var token = Token(registry);
        var outcomes = new ActionOutcome[20];
        Parallel.For(0, outcomes.Length, i => outcomes[i] = registry.Consume(token, out _));
        Assert.Equal(1, outcomes.Count(o => o == ActionOutcome.Success));
    }
    [Fact]
    public void TokenCannotCrossRegistryBoundary()
    {
        var token = Token(new IntentRegistry(clock));
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, new IntentRegistry(clock).Consume(token, out _));
    }
}
