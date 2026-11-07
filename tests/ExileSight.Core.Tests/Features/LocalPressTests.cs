using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class LocalPressTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalHoldPassesBothEdgesWithoutIssuingAuthority(bool declaresAction)
    {
        var feature = new LocalFeature(declaresAction);
        var host = new FeatureHost([feature], (_, _) => null, (_, _, _) => { },
            (_, _, _) => throw new InvalidOperationException("Gate must not be reached"));
        var config = new HotkeyConfiguration();
        var mapped = BindingMapper.Map(host.DefaultBindings.Select(b => b.ToSetting()), config, host.Commands);
        var binding = Assert.Single(mapped.Bindings);
        Assert.Null(binding.CommandAction); Assert.True(binding.TrackRelease); Assert.False(binding.Block);
        var clock = new Clock();
        var dispatcher = new HotkeyDispatcher(new IntentRegistry(clock, new object()), mapped.Bindings);
        dispatcher.InstallFeatureIssuer((_, _) => throw new InvalidOperationException("No authority"));
        var command = Assert.IsAssignableFrom<IPressCommandHandler>(Assert.Single(host.Commands));
        var router = new PressRouter(dispatcher, clock, () => true, (_, _) => throw new InvalidOperationException(),
            (token, press) => command.Press(token, new(press.Id, press.Timestamp)), (_, up) => command.Release(up));
        var down = dispatcher.Observe(new(9, true, false, 0, 0), true);
        Assert.False(down.Block); Assert.Equal(ActionOutcome.Success, router.Route(down.Press!).Outcome);
        var up = dispatcher.Observe(new(9, false, false, 0, 1), true);
        Assert.False(up.Block); Assert.Equal(ActionOutcome.Success, router.Route(up.Press!).Outcome);
        Assert.Equal(1, feature.Releases); Assert.Null(feature.Received);
        Assert.Equal(declaresAction ? ActionOutcome.MissingToken : ActionOutcome.Undeclared, feature.GateResult!.Outcome);
        command.Press(new IntentToken(), new(2, 2));
        Assert.Null(feature.Received);
    }
    private sealed class LocalFeature(bool declaresAction) : IFeature
    {
        public IntentToken? Received; public int Releases; public ActionResult? GateResult;
        public FeatureManifest Manifest { get; } = new("local", [Game.PoE1], declaresAction ? [ActionKind.CopyItem] : [], [], [], "Local hold");
        public void Register(IFeatureContext c)
        {
            c.RegisterPressCommand("hold", null, (token, _) =>
            {
                Received = token; GateResult = c.Execute(token, new GameAction.CopyItem());
                return new(ActionOutcome.Success);
            }, _ => Releases++);
            c.RegisterDefaultBinding(new("local.hold", 9, 0, false, "local:hold"));
        }
    }
    private sealed class Clock : IClock
    {
        public long Milliseconds => 0; public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public void Delay(int milliseconds) { }
    }
}
