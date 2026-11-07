using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Input;
using ExileSight.Core.Logs;
using ExileSight.Core.Tests.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class FeatureHardeningTests
{
    [Theory]
    [InlineData("builtin")]
    [InlineData("BUILTIN")]
    [InlineData("BuiltIn")]
    public void BuiltinIdentityIsReserved(string id) => Assert.Throws<ArgumentException>(() => Manifest(id));
    [Fact]
    public void SubscriberFailureIsReportedWithoutSkippingNextSubscriber()
    {
        var received = 0;
        var host = Host([new Module("one", c => c.Subscribe<AreaEntered>(_ => throw new InvalidOperationException())),
            new Module("two", c => c.Subscribe<AreaEntered>(_ => received++))]);
        var errors = new List<string>();
        Assert.True(host.HasLogSubscriptions);
        host.Publish(new AreaEntered("", null, "Town"), (id, _) => errors.Add(id));
        Assert.Equal(["one"], errors);
        Assert.Equal(1, received);
        Assert.False(Host([]).HasLogSubscriptions);
    }
    [Fact]
    public void ContextCannotSpendAnotherFeaturesTokenEvenWithSameDeclaredAction()
    {
        var clock = new FakeClock(); var authority = new object();
        var registry = new IntentRegistry(clock, authority);
        var policy = new GatePolicy(registry, [], [Manifest("one"), Manifest("two")]);
        IFeatureContext? second = null;
        _ = new FeatureHost([new Module("two", c => second = c)], (_, _) => null, (_, _, _) => { },
            (id, token, action) => new(policy.Validate(token, action, true, out _, id)));
        var token = registry.Register(authority, clock.Milliseconds, "one:copy", ActionKind.CopyItem, "one");
        Assert.Equal(ActionOutcome.Undeclared, second!.Execute(token, new GameAction.CopyItem()).Outcome);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, policy.Validate(token, new GameAction.CopyItem(), true, out _, "one"));
        var own = registry.Register(authority, clock.Milliseconds, "two:copy", ActionKind.CopyItem, "two");
        Assert.Equal(ActionOutcome.Success, second.Execute(own, new GameAction.CopyItem()).Outcome);
    }
    [Fact]
    public void FeatureIssuerCannotBeInstalledTwiceOrReplacedByLegacyIssuer()
    {
        var dispatcher = new HotkeyDispatcher(new IntentRegistry(new FakeClock()), []);
        dispatcher.InstallFeatureIssuer((_, _) => new());
        Assert.Throws<InvalidOperationException>(() => dispatcher.InstallFeatureIssuer((_, _) => new()));
        Assert.Throws<InvalidOperationException>(() => dispatcher.InstallIssuer((_, _, _) => new()));
        var legacy = new HotkeyDispatcher(new IntentRegistry(new FakeClock()), []);
        legacy.InstallIssuer((_, _, _) => new());
        Assert.Throws<InvalidOperationException>(() => legacy.InstallFeatureIssuer((_, _) => new()));
    }
    private static FeatureHost Host(IEnumerable<IFeature> features) => new(features, (_, _) => null, (_, _, _) => { });
    private static FeatureManifest Manifest(string id) => new(id, [Game.PoE1], [ActionKind.CopyItem], [], [], "Tests");
    private sealed class Module(string id, Action<IFeatureContext> register) : IFeature
    {
        public FeatureManifest Manifest { get; } = FeatureHardeningTests.Manifest(id);
        public void Register(IFeatureContext context) => register(context);
    }
}
