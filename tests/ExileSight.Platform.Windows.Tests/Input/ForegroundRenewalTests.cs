using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Input;
using ExileSight.Core.Items;
using ExileSight.Features.Macros;
using ExileSight.Features.Omni;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Input;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Input;

public sealed class ForegroundRenewalTests
{
    [Theory]
    [InlineData(false, 100)]
    [InlineData(false, 201)]
    [InlineData(true, 100)]
    [InlineData(true, 201)]
    public void StalledRenewalStillRoutesRealOmniAndMacroRelease(bool macro, int duration)
    {
        var omni = new OmniFeature();
        var macros = new MacrosFeature();
        var tooltip = new TooltipRoute();
        var sends = 0;
        var menus = 0;
        omni.Menu.Opened += _ => menus++;
        macros.Wheel.Opened += (_, _, _) => menus++;
        IntentToken? issued = null;
        var host = new FeatureHost([omni, macros, tooltip], (_, _) => null, (_, _, _) => { },
            (_, token, action) =>
            {
                Assert.Same(issued, token);
                sends++;
                if (macro) Assert.Equal("/hideout", Assert.IsType<GameAction.SendChatCommand>(action).Text);
                else Assert.IsType<GameAction.CopyItem>(action);
                return new(ActionOutcome.Success, CopiedText: File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "item.txt")));
            }, dispatch: action => action());
        var command = (IPressCommandHandler)host.Commands.Single(c => c.Command == (macro ? "macros:travel" : "omni:press"));
        var clock = new Clock();
        var config = new HotkeyConfiguration();
        config.Replace([new("test", 0x14, 0, true, null, FeatureCommand: command.Command,
            FeatureId: command.FeatureId, CommandAction: command.RequiredAction, TrackRelease: true)], []);
        var dispatcher = new HotkeyDispatcher(new IntentRegistry(clock), config);
        dispatcher.InstallFeatureIssuer((_, _) => issued = new());
        using var foreground = new HookForeground(new Window(), config, clock, () => new(42, 7), _ => 7);
        foreground.Start();
        foreground.Refresh();
        var router = new PressRouter(dispatcher, clock, () => true, (_, _) => throw new InvalidOperationException(),
            (token, press) => command.Press(token, new(press.Id, press.Timestamp)), (_, release) => command.Release(release));
        var down = dispatcher.Observe(new(0x14, true, false, 0, 100), true, !foreground.Evaluate().Allowed);
        router.Route(down.Press!);
        clock.Delay(600); // callback/renewal stall; native physical timestamps are unchanged
        var focus = foreground.Evaluate();
        Assert.True(focus.Reevaluated);
        Assert.True(focus.Allowed);
        Assert.Equal(600, focus.CachedAge);
        var up = dispatcher.Observe(new(0x14, false, false, 0, 100 + duration), true, !focus.Allowed);
        Assert.True(up.Block);
        Assert.Equal(ActionOutcome.Success, router.Route(up.Press!).Outcome);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, router.Route(up.Press!).Outcome);
        Assert.Equal(macro && duration > 200 ? 0 : 1, sends);
        Assert.Equal(duration > 200 ? 1 : 0, menus);
        Assert.Equal(!macro && duration <= 200 ? 1 : 0, tooltip.Calls);
    }
    [Theory]
    [InlineData(99, 7, 100)]
    [InlineData(99, 7, 600)]
    [InlineData(42, 8, 100)]
    [InlineData(42, 8, 600)]
    public void GenuineWindowOrPidChangePassesAndDropsClaimedRelease(int handle, uint pid, int delay)
    {
        var clock = new Clock();
        var config = new HotkeyConfiguration();
        config.Replace([new("copy", 0x14, 0, true, new GameAction.CopyItem(), TrackRelease: true)], []);
        var native = new NativeForeground(42, 7);
        using var foreground = new HookForeground(new Window(), config, clock, () => native, _ => 7);
        foreground.Start(); foreground.Refresh();
        var dispatcher = new HotkeyDispatcher(new IntentRegistry(clock), config);
        Assert.True(dispatcher.Observe(new(0x14, true, false, 0, 100), true, !foreground.Evaluate().Allowed).Block);
        native = new(handle, pid);
        clock.Delay(delay);
        var focus = foreground.Evaluate();
        Assert.False(focus.Allowed);
        var repeat = dispatcher.Observe(new(0x14, true, false, 0, 150), true, !focus.Allowed);
        var up = dispatcher.Observe(new(0x14, false, false, 0, 200), true, !focus.Allowed);
        Assert.False(repeat.Block);
        Assert.False(up.Block);
        Assert.Null(repeat.Press);
        Assert.Null(up.Press);
        Assert.Null(dispatcher.Observe(new(0x14, true, false, 0, 300), true, !focus.Allowed).Press);
    }
    [Fact]
    public void BlockedUiNeedsNoPostsAndEachDecisionUsesOneBoundedProbe()
    {
        var previous = SynchronizationContext.Current;
        var context = new BlockedContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var clock = new Clock();
            var probes = 0;
            using var foreground = new HookForeground(new Window(), new(), clock,
                () => { probes++; return new(42, 7); }, _ => 7);
            foreground.Start(); foreground.Refresh();
            clock.Delay(1000);
            Assert.Equal(1, probes); // advancing idle time performs no work
            Assert.True(foreground.IsStale);
            Assert.True(foreground.Evaluate().Allowed);
            Assert.Equal(2, probes);
            Assert.False(foreground.IsStale);
            Assert.False(foreground.Evaluate().Reevaluated);
            Assert.Equal(3, probes);
            Assert.Equal(0, context.Posts);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    [Theory]
    [InlineData(100)]
    [InlineData(600)]
    public void MinimizedGameCannotClaimAndClearsFocusedContext(int delay)
    {
        var clock = new Clock();
        var config = new HotkeyConfiguration();
        config.Replace([new("copy", 0x14, 0, true, new GameAction.CopyItem())], []);
        var native = new NativeForeground(42, 7);
        using var foreground = new HookForeground(new Window(), config, clock, () => native, _ => 7);
        foreground.Start(); foreground.Refresh();
        Assert.Contains("gameFocused", config.ActiveContexts);
        native = native with { IsMinimized = true };
        clock.Delay(delay);
        Assert.False(foreground.Evaluate().Allowed);
        Assert.DoesNotContain("gameFocused", config.ActiveContexts);
        var dispatcher = new HotkeyDispatcher(new IntentRegistry(clock), config);
        var down = dispatcher.Observe(new(0x14, true, false, 0, clock.Milliseconds), true);
        Assert.False(down.Block);
        Assert.Null(down.Press);
    }
    private sealed class TooltipRoute : IFeature, IOmniHandler
    {
        public int Calls;
        public FeatureManifest Manifest { get; } = new("test", [Game.PoE1], [], [], [], "test");
        public void Register(IFeatureContext context) => context.RegisterOmniHandler(this);
        public int Priority => 100;
        public bool Matches(ParsedItem item, OmniContext context) => true;
        public void Handle(ParsedItem item, OmniContext context, OmniPressKind press)
        { Assert.Equal(OmniPressKind.Short, press); Calls++; }
    }
    private sealed class BlockedContext : SynchronizationContext
    {
        public int Posts;
        public override void Post(SendOrPostCallback d, object? state) => Posts++;
    }
    private sealed class Clock : IClock
    {
        public long Milliseconds { get; private set; } = 100;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public void Delay(int milliseconds) => Milliseconds += milliseconds;
    }
    private sealed class Window : IGameWindow
    {
        public GameWindowState Current => new(true, 42, default, 96, true, false, default);
        public string? LastError => null;
        public event EventHandler? Changed { add { } remove { } }
        public void Start() { }
        public void Dispose() { }
    }
}
