using ExileSight.Platform.Windows.Window;
using Windows.Win32;

namespace ExileSight.Platform.Windows.Input;

public static class GameForeground
{
    public static unsafe bool IsGame()
    {
        var window = PInvoke.GetForegroundWindow();
        return !window.IsNull && GameWindowDetection.Shared.Matches((nint)window.Value) &&
            window == PInvoke.GetForegroundWindow();
    }
}
