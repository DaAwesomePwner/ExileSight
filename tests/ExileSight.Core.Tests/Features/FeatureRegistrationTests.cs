using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Input;
using ExileSight.Core.Items;
using ExileSight.Core.Logs;
using ExileSight.Core.Settings;
using ExileSight.Core.Tests.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class FeatureRegistrationTests
{
    [Fact]
    public void RegisteredCommandFlowsFromBindingThroughPhysicalIssuerToGate()
    {
        var clock = new FakeClock(); var authority = new object();
        var registry = new IntentRegistry(clock, authority);
        GatePolicy? gate = null;
        var feature = new Module(c => c.RegisterCommand("copy", ActionKind.CopyItem,
            token => c.Execute(token, new GameAction.CopyItem())));
        var host = new FeatureHost([feature], (_, _) => null, (_, _, _) => { },
            (id, token, action) => new(gate!.Validate(token, action, true, out _, id)));
        gate = new(registry, [], host.Manifests);
        var config = new HotkeyConfiguration();
        var bindings = BindingMapper.Map([new PersistedBinding { Id = "key", Key = 118, FeatureCommand = "module:copy" }], config, host.Commands);
        var binding = Assert.Single(bindings.Bindings);
        Assert.Equal("module", binding.FeatureId);
        var dispatcher = new HotkeyDispatcher(registry, bindings.Bindings);
        dispatcher.InstallFeatureIssuer((time, b) => registry.Register(authority, time, b.Id, b.CommandAction!.Value, b.FeatureId));
        var execution = new HotkeyExecution(new NoDirectActions(), host.Commands);
        var router = new PressRouter(dispatcher, clock, () => true, execution.Execute);
        var press = dispatcher.Observe(new(118, true, false, 0, clock.Milliseconds), true).Press!;
        Assert.Equal(ActionOutcome.Success, router.Route(press).Outcome);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, router.Route(press).Outcome);
    }
    [Fact]
    public void OmniIsPriorityOrderedFirstMatchAndSubscriptionsAreTyped()
    {
        var handled = new List<int>(); var events = 0; IFeatureContext? context = null;
        var host = new FeatureHost([new Module(c =>
        {
            context = c;
            c.RegisterOmniHandler(new Handler(1, true, handled));
            c.RegisterOmniHandler(new Handler(10, false, handled));
            c.RegisterOmniHandler(new Handler(5, true, handled));
            c.Subscribe<AreaEntered>(_ => events++);
            c.RegisterOverlay(handled);
        })], (_, _) => null, (_, _, _) => { });
        Assert.True(host.Route(new ParsedItem(), new(Game.PoE1), OmniPressKind.Long));
        Assert.Equal([5], handled);
        host.Publish(new Unknown("", null)); host.Publish(new AreaEntered("", null, "Town"));
        Assert.Equal(1, events);
        Assert.Same(handled, Assert.Single(host.Overlays).ViewModel);
        Assert.Throws<InvalidOperationException>(() => context!.RegisterOverlay(new object()));
    }
    [Fact]
    public void SettingsAccessIsScopedAndUndeclaredCommandRegistrationFails()
    {
        var keys = new List<string>();
        using var json = JsonDocument.Parse("123");
        _ = new FeatureHost([new Module(c => { c.WriteSetting("value", json.RootElement); c.ReadSetting("value"); })],
            (id, key) => { keys.Add(id + ":" + key); return null; },
            (id, key, _) => keys.Add(id + ":" + key));
        Assert.Equal(["module:value", "module:value"], keys);
        Assert.Throws<ArgumentException>(() => new FeatureHost([new Module(c => c.RegisterCommand("price",
            ActionKind.SetPriceNote, _ => new(ActionOutcome.Success)))], (_, _) => null, (_, _, _) => { }));
    }
    private sealed class Module(Action<IFeatureContext> register) : IFeature
    {
        public FeatureManifest Manifest { get; } = new("module", [Game.PoE1], [ActionKind.CopyItem], [], [], "Tests");
        public void Register(IFeatureContext context) => register(context);
    }
    private sealed class Handler(int priority, bool match, List<int> handled) : IOmniHandler
    {
        public int Priority => priority;
        public bool Matches(ParsedItem item, OmniContext context) => match;
        public void Handle(ParsedItem item, OmniContext context, OmniPressKind press) => handled.Add(priority);
    }
    private sealed class NoDirectActions : IActionGate
    {
        public string AuditPath => "";
        public int ActionsSent => 0;
        public ActionResult TryExecute(IntentToken? token, GameAction action) => throw new InvalidOperationException();
    }
}
