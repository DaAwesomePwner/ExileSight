using ExileSight.Core.Input;
using Windows.Win32;

namespace ExileSight.Platform.Windows.Input;

public sealed partial class HookHost
{
    public PassiveInputChannel Passive { get; } = new();
    public long InputTimestamp => clock.Milliseconds;
    public static unsafe (int X, int Y) CursorPosition()
    {
        System.Drawing.Point point;
        if (!PInvoke.GetCursorPos(&point)) throw new InvalidOperationException("Cursor unavailable");
        return (point.X, point.Y);
    }
}
