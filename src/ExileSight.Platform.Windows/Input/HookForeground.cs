using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using Windows.Win32;

namespace ExileSight.Platform.Windows.Input;

/// <summary>Renewed on the existing hook loop, never via the UI synchronization context.</summary>
internal sealed class HookForeground(IGameWindow window, HotkeyConfiguration configuration, IClock clock) : IDisposable
{
    private readonly ForegroundLease lease = new();
    internal void Start() { }
    internal unsafe void Refresh() => Refresh((nint)PInvoke.GetForegroundWindow().Value);
    internal void Refresh(nint nativeForeground)
    {
        var state = window.Current;
        // The tracker identifies the game; current native focus, not its UI snapshot,
        // establishes freshness. A minimized window cannot be the foreground window.
        var focused = state.Found && state.Handle != 0 && state.Handle == nativeForeground;
        configuration.UpdateGameFocused(focused, state.Handle);
        lease.Publish(state.Handle, focused, clock.Milliseconds);
    }
    internal long Age => lease.Age(clock.Milliseconds);
    internal bool IsStale => lease.IsStale(clock.Milliseconds);
    internal unsafe bool AllowsBlocking() => lease.AllowsBlocking(clock.Milliseconds, (nint)PInvoke.GetForegroundWindow().Value);
    public void Dispose() { }
}
