using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using Windows.Win32;

namespace ExileSight.Platform.Windows.Input;

/// <summary>UI heartbeat renews tracker state. Stalled UI and changed native foreground pass keys through.</summary>
internal sealed class HookForeground(IGameWindow window, HotkeyConfiguration configuration, IClock clock) : IDisposable
{
    private readonly ForegroundLease lease = new();
    private SynchronizationContext? context;
    private Timer? timer;
    private int pending, disposed;
    internal void Start()
    {
        context = SynchronizationContext.Current;
        window.Changed += Changed;
        Changed(null, EventArgs.Empty);
        // Without a UI context no heartbeat is possible: expire safely after the initial update.
        if (context is not null) timer = new Timer(_ => Queue(), null, 100, 100);
    }
    private void Queue()
    {
        if (Interlocked.Exchange(ref pending, 1) != 0) return;
        context!.Post(_ =>
        {
            Interlocked.Exchange(ref pending, 0);
            if (Volatile.Read(ref disposed) == 0) Changed(null, EventArgs.Empty);
        }, null);
    }
    private void Changed(object? sender, EventArgs args)
    {
        var state = window.Current;
        var focused = state.Found && state.IsForeground && !state.IsMinimized;
        configuration.UpdateGameFocused(focused, state.Handle);
        lease.Publish(state.Handle, focused, clock.Milliseconds);
    }
    internal unsafe bool AllowsBlocking() => lease.AllowsBlocking(clock.Milliseconds, (nint)PInvoke.GetForegroundWindow().Value);
    public void Dispose()
    {
        Interlocked.Exchange(ref disposed, 1);
        window.Changed -= Changed;
        timer?.Dispose();
    }
}
