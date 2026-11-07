using ExileSight.Platform.Windows.Window;
using Windows.Win32;

namespace ExileSight.Platform.Windows.Input;

public static class GameForeground
{
    public static unsafe bool IsStreaming()
    {
        var window = PInvoke.GetForegroundWindow();
        char* name = stackalloc char[256];
        var count = PInvoke.GetClassName(window, name, 256);
        // Unknown clients use the longer hold; never shorten a streaming paste.
        return new string(name, 0, Math.Max(0, count)) != "POEWindowClass";
    }
    public static unsafe bool IsGame()
    {
        var window = PInvoke.GetForegroundWindow();
        return !window.IsNull && GameWindowDetection.Shared.Matches((nint)window.Value) &&
            window == PInvoke.GetForegroundWindow();
    }
}
