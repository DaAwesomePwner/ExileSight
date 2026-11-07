using ExileSight.Platform.Abstractions.Window;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace ExileSight.Platform.Windows.Window;

/// <summary>Out-of-context hooks live on the caller's UI message loop; callbacks only enqueue refreshes.</summary>
public sealed class GameWindowTracker : IGameWindow
{
    private readonly GameWindowHooks hooks;
    private string? lastError;
    public string? LastError => Volatile.Read(ref lastError);
    private readonly WINEVENTPROC callback;
    private SynchronizationContext? context;
    private Timer? retry;
    private GameWindowState current = GameWindowState.Missing;
    private int pending;
    private bool disposed;
    private bool streaming;
    public GameWindowState Current => Volatile.Read(ref current);
    public event EventHandler? Changed;

    public GameWindowTracker()
    {
        callback = OnEvent;
        hooks = new GameWindowHooks(callback);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (context is not null) return;
        context = SynchronizationContext.Current ??
            throw new InvalidOperationException("Start requires a UI synchronization context and message loop.");
        try
        {
            hooks.Start();
            retry = new Timer(_ => QueueRefresh(), null, Timeout.Infinite, Timeout.Infinite);
            Refresh();
        }
        catch { Dispose(); throw; }
    }

    private unsafe void OnEvent(HWINEVENTHOOK hook, uint eventId, HWND hwnd, int objectId,
        int childId, uint thread, uint time)
    {
        if (eventId == WinEvents.Foreground || ((nint)hwnd.Value == Current.Handle && objectId == 0 && childId == 0))
            QueueRefresh();
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
        try { RefreshCore(); }
        catch (Exception error)
        {
            // Missing state notifies the host to hide, even if hook installation keeps failing.
            Publish(GameWindowState.Missing with { LastEventTime = DateTimeOffset.UtcNow }, error.Message);
        }
    }

    private unsafe void RefreshCore()
    {
        HWND hwnd = new(Current.Handle);
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd) ||
            (streaming && !GameWindowDetection.HasGameTitle(hwnd)))
            hwnd = GameWindowDetection.Find(out streaming);
        var next = GameWindowState.Missing with { LastEventTime = DateTimeOffset.UtcNow };
        if (!hwnd.IsNull)
        {
            RECT rect;
            System.Drawing.Point origin = default;
            if (PInvoke.GetClientRect(hwnd, &rect) && PInvoke.ClientToScreen(hwnd, &origin))
                next = new(true, (nint)hwnd.Value, new(origin.X, origin.Y,
                    rect.right - rect.left, rect.bottom - rect.top),
                    Math.Max(96u, PInvoke.GetDpiForWindow(hwnd)),
                    PInvoke.GetForegroundWindow() == hwnd, PInvoke.IsIconic(hwnd), DateTimeOffset.UtcNow);
        }
        uint pid = 0;
        if (next.Found) PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        hooks.ScopeTo(pid);
        Publish(next, null);
    }

    private void Publish(GameWindowState next, string? error)
    {
        Volatile.Write(ref lastError, error);
        Volatile.Write(ref current, next);
        retry?.Change(next.Found ? Timeout.Infinite : 1500, Timeout.Infinite);
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
