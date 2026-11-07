using System.ComponentModel;
using System.Diagnostics;
using ExileSight.Core.Overlay;
using ExileSight.Platform.Abstractions.Window;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace ExileSight.Platform.Windows.Window;

/// <summary>Out-of-context hooks live on the caller's UI message loop; callbacks only enqueue refreshes.</summary>
public sealed class GameWindowTracker : IGameWindow
{
    private readonly List<HWINEVENTHOOK> hooks = [];
    private readonly WINEVENTPROC callback;
    private SynchronizationContext? context;
    private Timer? retry;
    private GameWindowState current = GameWindowState.Missing;
    private int pending;
    private bool disposed;
    public GameWindowState Current => Volatile.Read(ref current);
    public event EventHandler? Changed;

    public GameWindowTracker() => callback = OnEvent;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (context is not null) return;
        context = SynchronizationContext.Current ??
            throw new InvalidOperationException("Start requires a UI synchronization context and message loop.");
        try
        {
            foreach (uint eventId in new uint[] { 3, 0x800B, 0x16, 0x17, 0x8001 })
            {
                var hook = PInvoke.SetWinEventHook(eventId, eventId, default, callback,
                    0, 0, PInvoke.WINEVENT_OUTOFCONTEXT);
                if (hook.IsNull) throw new Win32Exception();
                hooks.Add(hook);
            }
            retry = new Timer(_ => QueueRefresh(), null, Timeout.Infinite, Timeout.Infinite);
            Refresh();
        }
        catch { Dispose(); throw; }
    }

    private unsafe void OnEvent(HWINEVENTHOOK hook, uint eventId, HWND hwnd, int objectId,
        int childId, uint thread, uint time)
    {
        if (eventId == 3 || ((nint)hwnd.Value == Current.Handle && objectId == 0 && childId == 0))
            QueueRefresh();
    }

    private void QueueRefresh()
    {
        if (Interlocked.Exchange(ref pending, 1) != 0) return;
        context?.Post(_ =>
        {
            Interlocked.Exchange(ref pending, 0);
            if (!disposed) Refresh();
        }, null);
    }

    private unsafe void Refresh()
    {
        HWND hwnd = new(Current.Handle);
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd)) hwnd = Find();
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
        Volatile.Write(ref current, next);
        retry?.Change(next.Found ? Timeout.Infinite : 1500, Timeout.Infinite);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static unsafe HWND Find()
    {
        HWND result = default;
        PInvoke.EnumWindows((hwnd, _) =>
        {
            if (!PInvoke.IsWindowVisible(hwnd)) return true;
            char* buffer = stackalloc char[256];
            int length = PInvoke.GetClassName(hwnd, buffer, 256);
            string windowClass = new(buffer, 0, Math.Max(0, length));
            if (OverlayRules.Matches(windowClass, null)) { result = hwnd; return false; }
            uint pid = 0;
            PInvoke.GetWindowThreadProcessId(hwnd, &pid);
            try
            {
                using var process = Process.GetProcessById((int)pid);
                if (OverlayRules.Matches(null, process.ProcessName)) { result = hwnd; return false; }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
            return true;
        }, default);
        return result;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        retry?.Dispose();
        foreach (var hook in hooks) PInvoke.UnhookWinEvent(hook);
        hooks.Clear();
        GC.KeepAlive(callback);
    }
}
