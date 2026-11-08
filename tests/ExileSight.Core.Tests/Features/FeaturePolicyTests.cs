using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Input;
using ExileSight.Core.Tests.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Shell;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class FeaturePolicyTests
{
    private static FeatureManifest Manifest(string id, params ActionKind[] actions) =>
        new(id, [Game.PoE1], actions, [], [], "Test feature");
    [Theory]
    [InlineData("price", ActionOutcome.Success)]
    [InlineData("other", ActionOutcome.Undeclared)]
    [InlineData("unknown", ActionOutcome.Undeclared)]
    [InlineData("builtin", ActionOutcome.Undeclared)]
    public void ActionsAreScopedToIssuingFeatureAndConsumed(string owner, ActionOutcome expected)
    {
        var clock = new FakeClock(); var authority = new object();
        var registry = new IntentRegistry(clock, authority);
        var gate = new GatePolicy(registry, [ActionKind.CopyItem, ActionKind.SendChatCommand],
            [Manifest("price", ActionKind.SetPriceNote), Manifest("other", ActionKind.CopyItem)]);
        var token = registry.Register(authority, clock.Milliseconds, "press", ActionKind.SetPriceNote, owner);
        Assert.Equal(expected, gate.Validate(token, new GameAction.SetPriceNote("~b/o 1 chaos"), true, out _));
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, gate.Validate(token, new GameAction.SetPriceNote("x"), true, out _));
    }
    [Fact]
    public void ManifestRejectsUnknownActionsHostsAndDuplicateIds()
    {
        Assert.Throws<ArgumentException>(() => Manifest("bad", (ActionKind)999));
        Assert.Throws<ArgumentException>(() => new FeatureManifest("bad", [Game.PoE1], [], [], ["pathofexile.com"], "bad"));
        Assert.Throws<ArgumentException>(() => new FeatureHost([new Feature("one"), new Feature("ONE")], (_, _) => null, (_, _, _) => { }));
    }
    [Fact]
    public void FeatureCommandsRetainOwnerAndRequireToken()
    {
        var host = new FeatureHost([new Feature("one")], (_, _) => null, (_, _, _) => { });
        var command = Assert.Single(host.Commands);
        Assert.Equal("one:copy", command.Command);
        Assert.Equal("one", command.FeatureId);
        Assert.Equal(ActionKind.CopyItem, command.RequiredAction);
        Assert.Equal(ActionOutcome.MissingToken, command.Execute().Outcome);
    }
    [Theory]
    [InlineData("http://poewiki.net/x")]
    [InlineData("https://poewiki.net.evil.com/x")]
    [InlineData("https://evil.com@poewiki.net/x")]
    [InlineData("https://poewiki.net:444/x")]
    [InlineData("file:///tmp/x")]
    [InlineData("https://poewiki.net\\@evil.com/x")]
    public void BrowserRejectsUnsafeUrls(string url) =>
        Assert.Throws<ArgumentException>(() => BrowserUrl.Validate(url, ["poewiki.net"]));
    [Fact]
    public void BrowserAllowsExactHttpsHost() =>
        Assert.Equal("poewiki.net", BrowserUrl.Validate("https://poewiki.net/wiki/Item?x=1", ["poewiki.net"]).Host);
    private sealed class Feature(string id) : IFeature
    {
        public FeatureManifest Manifest { get; } = FeaturePolicyTests.Manifest(id, ActionKind.CopyItem);
        public void Register(IFeatureContext context) => context.RegisterCommand("copy", ActionKind.CopyItem, _ => new(ActionOutcome.Success));
    }
}
