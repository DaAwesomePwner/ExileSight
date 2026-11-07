using System.ComponentModel;
using Windows.Win32;
using Windows.Win32.UI.Accessibility;

namespace ExileSight.Platform.Windows.Window;

internal sealed class GameWindowHooks(WINEVENTPROC callback) : IDisposable
{
    private HWINEVENTHOOK foreground;
    private readonly List<HWINEVENTHOOK> scoped = [];
    private uint scopedPid;

    internal void Start()
    {
        // Do not skip our process: focusing ExileSight must hide the overlay too.
        foreground = PInvoke.SetWinEventHook(WinEvents.Foreground, WinEvents.Foreground,
            default, callback, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT);
        if (foreground.IsNull) throw new Win32Exception();
    }

    internal void ScopeTo(uint pid)
    {
        if (pid == scopedPid) return;
        ClearScope();
        if (pid == 0) return; // Never turn game-only events into global hooks.
        try
        {
            foreach (uint eventId in new[] { WinEvents.LocationChange, WinEvents.MinimizeStart,
                WinEvents.MinimizeEnd, WinEvents.Destroy })
            {
                var hook = PInvoke.SetWinEventHook(eventId, eventId, default, callback,
                    pid, 0, PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
                if (hook.IsNull) throw new Win32Exception();
                scoped.Add(hook);
            }
            scopedPid = pid;
        }
        catch { ClearScope(); throw; }
    }

    private void ClearScope()
    {
        foreach (var hook in scoped) PInvoke.UnhookWinEvent(hook);
        scoped.Clear();
        scopedPid = 0;
    }

    public void Dispose()
    {
        ClearScope();
        if (!foreground.IsNull) PInvoke.UnhookWinEvent(foreground);
        foreground = default;
        GC.KeepAlive(callback);
    }
}
