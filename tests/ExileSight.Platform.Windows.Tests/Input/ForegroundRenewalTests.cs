using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Input;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Input;

public sealed class ForegroundRenewalTests
{
    [Fact]
    public void BlockedUiContextCannotPreventNativeFocusRenewal()
    {
        var previous = SynchronizationContext.Current;
        var context = new BlockedContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var clock = new Clock();
            using var foreground = new HookForeground(new Window(), new(), clock);
            foreground.Start();
            foreground.Refresh(42);
            clock.Delay(1000);
            Assert.True(foreground.IsStale);
            foreground.Refresh(42);
            Assert.False(foreground.IsStale);
            Assert.Equal(0, foreground.Age);
            Assert.Equal(0, context.Posts);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    [Fact]
    public void NativeFocusMismatchDisablesGameBindingEvenWithStaleUiFocus()
    {
        var clock = new Clock();
        var config = new HotkeyConfiguration();
        config.Replace([new("copy", 0x14, 0, true, new ExileSight.Platform.Abstractions.Input.GameAction.CopyItem())], []);
        using var foreground = new HookForeground(new Window(), config, clock);
        foreground.Refresh(99);
        var dispatcher = new HotkeyDispatcher(new IntentRegistry(clock), config);
        var decision = dispatcher.Observe(new(0x14, true, false, 0, 100), true);
        Assert.False(decision.Block);
        Assert.Null(decision.Press);
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
