using System.Collections.Concurrent;
using Windows.Win32;

namespace ExileSight.Platform.Windows.Input;

public sealed record PassiveInput(uint Message, int X, int Y);

public sealed partial class HookHost
{
    private readonly ConcurrentQueue<PassiveInput> passive = new();
    public event Action<PassiveInput>? Observed;
    public static unsafe (int X, int Y) CursorPosition()
    {
        System.Drawing.Point point;
        if (!PInvoke.GetCursorPos(&point)) throw new InvalidOperationException("Cursor unavailable");
        return (point.X, point.Y);
    }
    private void EnqueuePassive(uint message, int x = 0, int y = 0)
    {
        if (passive.Count >= 64) return;
        passive.Enqueue(new(message, x, y));
        ready.Set();
    }
    private void DrainPassive()
    {
        while (passive.TryDequeue(out var input)) Observed?.Invoke(input);
    }
}
