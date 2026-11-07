using System.Diagnostics;
using ExileSight.Core.Overlay;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ExileSight.Platform.Windows.Window;

internal static class GameWindowDetection
{
    internal static unsafe HWND Find(out bool streaming)
    {
        // Enumeration provides IDs without opening individual process handles.
        var processes = Process.GetProcessesByName("GeForceNOW");
        HashSet<uint> streamingPids;
        try { streamingPids = processes.Select(process => (uint)process.Id).ToHashSet(); }
        finally { foreach (var process in processes) process.Dispose(); }

        HWND result = default;
        bool resultStreaming = false;
        PInvoke.EnumWindows((hwnd, _) =>
        {
            if (!PInvoke.IsWindowVisible(hwnd)) return true;
            char* buffer = stackalloc char[1024];
            int length = PInvoke.GetClassName(hwnd, buffer, 1024);
            string windowClass = new(buffer, 0, Math.Max(0, length));
            uint pid = 0;
            PInvoke.GetWindowThreadProcessId(hwnd, &pid);
            bool isStreaming = OverlayRules.MatchesProcess(pid, streamingPids);
            string? title = null;
            if (isStreaming)
            {
                length = PInvoke.GetWindowText(hwnd, buffer, 1024);
                title = new(buffer, 0, Math.Max(0, length));
            }
            if (!OverlayRules.Matches(windowClass, isStreaming, title)) return true;
            result = hwnd;
            resultStreaming = !OverlayRules.Matches(windowClass, false, null);
            return false;
        }, default);
        streaming = resultStreaming;
        return result;
    }

    internal static unsafe bool HasGameTitle(HWND hwnd)
    {
        char* buffer = stackalloc char[1024];
        int length = PInvoke.GetWindowText(hwnd, buffer, 1024);
        return OverlayRules.IsStreamingGameTitle(new string(buffer, 0, Math.Max(0, length)));
    }
}
