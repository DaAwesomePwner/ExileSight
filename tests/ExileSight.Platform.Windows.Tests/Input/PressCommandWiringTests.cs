using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Input;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Input;

public sealed class PressCommandWiringTests
{
    [Fact]
    public void HookConsumerCorrelatesReleaseWithoutOverwritingActionDiagnostics()
    {
        var clock = new Clock();
        var binding = Binding();
        var dispatcher = new HotkeyDispatcher(new IntentRegistry(clock), [binding]);
        dispatcher.InstallFeatureIssuer((_, _) => new());
        var command = new TestCommand();
        var reports = new List<ActionResult>();
        using var hooks = new HookHost(dispatcher, clock, (_, _) => throw new InvalidOperationException("Wrong route"),
            new(), (_, result) => reports.Add(result), new GameWindow(), [command], () => true);
        var down = dispatcher.Observe(new(0x14, true, false, 0, 100), true).Press!;
        hooks.RouteAndReport(down);
        var up = dispatcher.Observe(new(0x14, false, false, 0, 350), true).Press!;
        hooks.RouteAndReport(up);
        Assert.Equal(new PressStarted(down.Id, 100), command.Started);
        Assert.Equal(new PressReleased(down.Id, 100, 350), command.Released);
        var result = Assert.Single(reports);
        Assert.True(result.AuditFailed);
        Assert.Equal("Original paste status", result.PasteStatus);
    }
    [Fact]
    public async Task BusyActionWorkerCannotBlockPassiveDismissal()
    {
        var clock = new Clock();
        var dispatcher = new HotkeyDispatcher(new IntentRegistry(clock), [Binding()]);
        dispatcher.InstallFeatureIssuer((_, _) => new());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var unblock = new ManualResetEventSlim();
        var command = new TestCommand { OnPress = () => { entered.SetResult(); Assert.True(unblock.Wait(TimeSpan.FromSeconds(30))); } };
        using var hooks = new HookHost(dispatcher, clock, (_, _) => throw new InvalidOperationException(),
            new(), gameWindow: new GameWindow(), commands: [command], gameFocused: () => true);
        var press = dispatcher.Observe(new(0x14, true, false, 0, 100), true).Press!;
        var actionWorker = Task.Factory.StartNew(() => hooks.RouteAndReport(press),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            hooks.Passive.Open(100);
            var dismissed = false;
            hooks.Passive.Observed += input => dismissed |= input.Message == 0x100;
            for (var i = 0; i < 8000; i++) hooks.Passive.Observe(0x200, i, i, 101);
            hooks.Passive.Observe(0x100, 0, 0, 102);
            hooks.Passive.Drain(); // UI consumer, while the actual HookHost action path is blocked
            Assert.True(dismissed);
            Assert.False(actionWorker.IsCompleted);
        }
        finally { unblock.Set(); await actionWorker.WaitAsync(TimeSpan.FromSeconds(30)); }
    }
    private static HotkeyBinding Binding() => new("omni.press", 0x14, 0, true, null,
        FeatureCommand: "omni:press", FeatureId: "omni", CommandAction: ActionKind.CopyItem, TrackRelease: true);
    private sealed class TestCommand : IPressCommandHandler
    {
        public string FeatureId => "omni";
        public string Command => "omni:press";
        public ActionKind? RequiredAction => ActionKind.CopyItem;
        public PressStarted? Started;
        public PressReleased? Released;
        public Action? OnPress;
        public ActionResult Execute() => throw new InvalidOperationException();
        public ActionResult Press(IntentToken? token, PressStarted press)
        {
            Assert.NotNull(token);
            Started = press;
            OnPress?.Invoke();
            return new(ActionOutcome.Success, AuditFailed: true, PasteStatus: "Original paste status");
        }
        public void Release(PressReleased release) => Released = release;
    }
    private sealed class Clock : IClock
    {
        public long Milliseconds => 100;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public void Delay(int milliseconds) { }
    }
    private sealed class GameWindow : IGameWindow
    {
        public GameWindowState Current => GameWindowState.Missing;
        public string? LastError => null;
        public event EventHandler? Changed { add { } remove { } }
        public void Start() { }
        public void Dispose() { }
    }
}
