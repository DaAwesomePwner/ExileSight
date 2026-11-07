using System.Diagnostics;
using ExileSight.Core.Overlay;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ExileSight.Platform.Windows.Window;

internal static class GameWindowDetection
{
    internal static unsafe HWND Find(out bool streaming)
    {
        var streamingPids = StreamingPids();

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

    private static HashSet<uint> StreamingPids()
    {
        // GetProcessesByName uses the OS process snapshot; Id does not open a process handle.
        var processes = Process.GetProcessesByName("GeForceNOW");
        try { return processes.Select(process => (uint)process.Id).ToHashSet(); }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    internal static unsafe bool IsStreamingGame(HWND hwnd)
    {
        char* buffer = stackalloc char[1024];
        int length = PInvoke.GetWindowText(hwnd, buffer, 1024);
        uint pid = 0;
        PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        return OverlayRules.MatchesStreaming(pid, StreamingPids(), new string(buffer, 0, Math.Max(0, length)));
    }
}
