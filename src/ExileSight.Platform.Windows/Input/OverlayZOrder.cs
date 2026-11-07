using ExileSight.Core.Input;
using Windows.Win32;

namespace ExileSight.Platform.Windows.Input;

/// <summary>Build on the UI thread, never inside a low-level callback. EnumWindows visits topmost first.</summary>
public static class OverlayZOrder
{
    public static unsafe IReadOnlyList<OverlayClickTarget> Snapshot(IEnumerable<OverlayClickTarget> targets)
    {
        var owned = targets.ToDictionary(t => t.Window);
        var ordered = new List<OverlayClickTarget>();
        PInvoke.EnumWindows((hwnd, _) =>
        {
            if (!owned.TryGetValue((nint)hwnd.Value, out var target)) return true;
            uint pid;
            PInvoke.GetWindowThreadProcessId(hwnd, &pid);
            if (pid == Environment.ProcessId)
                ordered.Add(target with { ZOrder = ordered.Count });
            return true;
        }, default);
        return ordered;
    }
}
