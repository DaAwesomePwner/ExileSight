using System.Diagnostics;
using ExileSight.Platform.Abstractions.Window;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ExileSight.Platform.Windows.Window;

internal sealed record WindowCandidate(nint Handle, uint Pid, string Class, string Title);

internal interface IWindowSource
{
    IReadOnlySet<uint> StreamingPids();
    IReadOnlyList<WindowCandidate> Enumerate(IReadOnlySet<uint> streamingPids);
    WindowCandidate? Read(nint handle, IReadOnlySet<uint> streamingPids);
    GameWindowState State(nint handle);
}

internal sealed class WindowSource : IWindowSource
{
    public IReadOnlySet<uint> StreamingPids()
    {
        var processes = Process.GetProcessesByName("GeForceNOW");
        try { return processes.Select(process => (uint)process.Id).ToHashSet(); }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    public unsafe IReadOnlyList<WindowCandidate> Enumerate(IReadOnlySet<uint> streamingPids)
    {
        var windows = new List<WindowCandidate>();
        PInvoke.EnumWindows((hwnd, _) =>
        {
            if (PInvoke.IsWindowVisible(hwnd) && Read((nint)hwnd.Value, streamingPids) is { } candidate)
                windows.Add(candidate);
            return true;
        }, default);
        return windows;
    }

    public unsafe WindowCandidate? Read(nint handle, IReadOnlySet<uint> streamingPids)
    {
        HWND hwnd = new(handle);
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd)) return null;
        char* buffer = stackalloc char[1024];
        int length = PInvoke.GetClassName(hwnd, buffer, 1024);
        string windowClass = new(buffer, 0, Math.Max(0, length));
        uint pid = 0;
        PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        string title = string.Empty;
        if (streamingPids.Contains(pid))
        {
            length = PInvoke.GetWindowText(hwnd, buffer, 1024);
            title = new(buffer, 0, Math.Max(0, length));
        }
        return new(handle, pid, windowClass, title);
    }

    public unsafe GameWindowState State(nint handle)
    {
        HWND hwnd = new(handle);
        RECT rect;
        System.Drawing.Point origin = default;
        if (!PInvoke.GetClientRect(hwnd, &rect) || !PInvoke.ClientToScreen(hwnd, &origin))
            return GameWindowState.Missing;
        return new(true, handle, new(origin.X, origin.Y, rect.right - rect.left, rect.bottom - rect.top),
            Math.Max(96u, PInvoke.GetDpiForWindow(hwnd)), PInvoke.GetForegroundWindow() == hwnd,
            PInvoke.IsIconic(hwnd), DateTimeOffset.UtcNow);
    }
}
