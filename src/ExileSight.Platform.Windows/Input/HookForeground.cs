using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ExileSight.Platform.Windows.Input;

internal readonly record struct NativeForeground(nint Handle, uint ProcessId, bool IsMinimized = false);
internal readonly record struct ForegroundDecision(bool Allowed, long CachedAge, bool Reevaluated);

/// <summary>Native focus freshness never depends on dispatching work to the UI.</summary>
internal sealed class HookForeground : IDisposable
{
    private readonly IGameWindow window;
    private readonly HotkeyConfiguration configuration;
    private readonly IClock clock;
    private readonly Func<NativeForeground> probe;
    private readonly Func<nint, uint> processId;
    private readonly ForegroundLease lease = new();
    private sealed record Identity(nint Handle, uint ProcessId);
    private Identity expected = new(0, 0);
    internal HookForeground(IGameWindow window, HotkeyConfiguration configuration, IClock clock,
        Func<NativeForeground>? probe = null, Func<nint, uint>? processId = null)
    {
        this.window = window; this.configuration = configuration; this.clock = clock;
        this.probe = probe ?? ReadForeground;
        this.processId = processId ?? ReadProcessId;
    }
    internal void Start()
    {
        window.Changed += CaptureIdentity;
        CaptureIdentity(null, EventArgs.Empty);
    }
    private void CaptureIdentity(object? sender, EventArgs args)
    {
        var state = window.Current;
        Volatile.Write(ref expected, state.Found && state.Handle != 0
            ? new(state.Handle, processId(state.Handle)) : new(0, 0));
    }
    internal void Refresh() => Refresh(probe());
    private void Refresh(NativeForeground native)
    {
        var identity = Volatile.Read(ref expected);
        var focused = Matches(identity, native);
        configuration.UpdateGameFocused(focused, identity.Handle);
        lease.Publish(identity.Handle, focused, clock.Milliseconds);
    }
    private static bool Matches(Identity identity, NativeForeground native) =>
        !native.IsMinimized && identity.Handle != 0 && identity.ProcessId != 0 &&
        identity.Handle == native.Handle && identity.ProcessId == native.ProcessId;
    internal ForegroundDecision Evaluate()
    {
        var age = lease.Age(clock.Milliseconds);
        var stale = lease.IsStale(clock.Milliseconds);
        // Fixed native queries only: no process enumeration, UI lock, timer, or worker wait.
        var native = probe();
        if (stale || native.IsMinimized) Refresh(native);
        return new(Matches(Volatile.Read(ref expected), native) &&
            lease.AllowsBlocking(clock.Milliseconds, native.Handle), age, stale);
    }
    internal long Age => lease.Age(clock.Milliseconds);
    internal bool IsStale => lease.IsStale(clock.Milliseconds);
    private static unsafe uint ReadProcessId(nint handle)
    {
        uint pid = 0;
        PInvoke.GetWindowThreadProcessId(new HWND(handle), &pid);
        return pid;
    }
    private static unsafe NativeForeground ReadForeground()
    {
        var handle = (nint)PInvoke.GetForegroundWindow().Value;
        var pid = ReadProcessId(handle);
        var minimized = PInvoke.IsIconic(new HWND(handle));
        return handle == (nint)PInvoke.GetForegroundWindow().Value ? new(handle, pid, minimized) : default;
    }
    public void Dispose() => window.Changed -= CaptureIdentity;
}
