using ExileSight.Platform.Abstractions.Window;
using ExileSight.Core.Overlay;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace ExileSight.Platform.Windows.Window;

/// <summary>Out-of-context hooks live on the caller's UI message loop; callbacks only enqueue refreshes.</summary>
public sealed class GameWindowTracker : IGameWindow
{
    private readonly IGameWindowHooks hooks;
    private readonly GameWindowDetection detection;
    private string? lastError;
    public string? LastError => Volatile.Read(ref lastError);
    private readonly WINEVENTPROC callback;
    private SynchronizationContext? context;
    private Timer? retry;
    private GameWindowState current = GameWindowState.Missing;
    private int pending;
    private bool disposed;
    public GameWindowState Current => Volatile.Read(ref current);
    public event EventHandler? Changed;

    public GameWindowTracker()
    {
        callback = OnEvent;
        hooks = new GameWindowHooks(callback);
        detection = new(new WindowSource(), TimeProvider.System);
    }

    internal GameWindowTracker(IGameWindowHooks hooks, GameWindowDetection detection)
    {
        callback = OnEvent;
        this.hooks = hooks;
        this.detection = detection;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (context is not null) return;
        context = SynchronizationContext.Current ??
            throw new InvalidOperationException("Start requires a UI synchronization context and message loop.");
        try
        {
            retry = new Timer(_ => QueueRefresh(), null, Timeout.Infinite, Timeout.Infinite);
            Refresh();
        }
        catch { Dispose(); throw; }
    }

    private unsafe void OnEvent(HWINEVENTHOOK hook, uint eventId, HWND hwnd, int objectId,
        int childId, uint thread, uint time)
        => HandleEvent(eventId, (nint)hwnd.Value, objectId, childId);

    internal void HandleEvent(uint eventId, nint handle, int objectId = 0, int childId = 0)
    {
        if (eventId == WinEvents.Foreground || (handle == Current.Handle && objectId == 0 && childId == 0))
        {
            if (eventId is WinEvents.NameChange or WinEvents.Destroy) detection.InvalidateSnapshot();
            QueueRefresh();
        }
    }

    private void QueueRefresh()
    {
        if (Interlocked.Exchange(ref pending, 1) != 0) return;
        context?.Post(_ =>
        {
            Interlocked.Exchange(ref pending, 0);
            if (disposed) return;
            Refresh();
        }, null);
    }

    private void Refresh()
    {
        var result = TrackingRefresh.Run(RefreshCore);
        Publish(result.State, result.Error);
    }

    private GameWindowState RefreshCore()
    {
        hooks.Start();
        var next = detection.Refresh(Current, out uint pid);
        hooks.ScopeTo(pid);
        return next;
    }

    private void Publish(GameWindowState next, string? error)
    {
        Volatile.Write(ref lastError, error);
        Volatile.Write(ref current, next);
        retry?.Change(new TrackingRefresh(next, error).RetryMilliseconds, Timeout.Infinite);
        foreach (EventHandler handler in Changed?.GetInvocationList() ?? [])
        {
            try { handler(this, EventArgs.Empty); }
            catch (Exception subscriberError) { Volatile.Write(ref lastError, subscriberError.Message); }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        retry?.Dispose();
        hooks.Dispose();
        GC.KeepAlive(callback);
    }
}
