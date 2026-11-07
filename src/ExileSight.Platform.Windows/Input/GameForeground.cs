using System.Diagnostics;
using ExileSight.Core.Input;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ExileSight.Platform.Windows.Input;

public static class GameForeground
{
    private static readonly object sync = new();
    private static nint lastWindow;
    private static HashSet<uint> geforceIds = [];

    // Callback reads an immutable cached snapshot; it never enumerates processes or takes the lock.
    // An unseen GFN process passes its first press through and requests a worker refresh.
    internal static unsafe bool IsCandidate(out bool refresh)
    {
        refresh = false;
        var hwnd = PInvoke.GetForegroundWindow();
        if (hwnd.IsNull) return false;
        var windowClass = WindowClass(hwnd);
        if (windowClass == "POEWindowClass") return true;
        uint pid;
        PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        // GetWindowText can message same-process windows. Never do that from the hook callback.
        if (pid == Environment.ProcessId) return false;
        var title = Title(hwnd);
        if (!title.Contains("Path of Exile", StringComparison.OrdinalIgnoreCase)) return false;
        refresh = (nint)hwnd.Value != Volatile.Read(ref lastWindow);
        return ForegroundRule.Matches(windowClass, pid, title, Volatile.Read(ref geforceIds));
    }
    public static unsafe bool IsGame()
    {
        var hwnd = PInvoke.GetForegroundWindow();
        if (hwnd.IsNull) return false;
        var windowClass = WindowClass(hwnd);
        // No process lookup whatsoever for the native game client.
        if (windowClass == "POEWindowClass")
        {
            lock (sync) Volatile.Write(ref lastWindow, (nint)hwnd.Value);
            return hwnd == PInvoke.GetForegroundWindow();
        }
        uint pid;
        PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        if (pid == Environment.ProcessId) return false;
        lock (sync)
        {
            if ((nint)hwnd.Value != lastWindow)
            {
                Volatile.Write(ref geforceIds, SnapshotGeForceIds());
                Volatile.Write(ref lastWindow, (nint)hwnd.Value);
            }
            return ForegroundRule.Matches(windowClass, pid, Title(hwnd), geforceIds) &&
                hwnd == PInvoke.GetForegroundWindow();
        }
    }
    private static HashSet<uint> SnapshotGeForceIds()
    {
        // GetProcessesByName uses the system process snapshot; Id is the snapshot ID, not a process handle.
        var ids = new HashSet<uint>();
        Process[] processes = [];
        try
        {
            processes = Process.GetProcessesByName("GeForceNOW");
            foreach (var process in processes) ids.Add((uint)process.Id);
        }
        catch (InvalidOperationException) { ids.Clear(); }
        catch (System.ComponentModel.Win32Exception) { ids.Clear(); }
        finally { foreach (var process in processes) process.Dispose(); }
        return ids;
    }
    private static unsafe string WindowClass(HWND hwnd)
    {
        char* buffer = stackalloc char[256];
        var count = PInvoke.GetClassName(hwnd, buffer, 256);
        return new string(buffer, 0, Math.Max(0, count));
    }
    private static unsafe string Title(HWND hwnd)
    {
        char* buffer = stackalloc char[512];
        var count = PInvoke.GetWindowText(hwnd, buffer, 512);
        return new string(buffer, 0, Math.Max(0, count));
    }
}
