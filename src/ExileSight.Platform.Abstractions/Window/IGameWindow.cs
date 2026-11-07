namespace ExileSight.Platform.Abstractions.Window;

public readonly record struct PixelRect(int X, int Y, int Width, int Height);
public sealed record GameWindowState(bool Found, nint Handle, PixelRect ClientRect,
    uint Dpi, bool IsForeground, bool IsMinimized, DateTimeOffset LastEventTime)
{
    public static GameWindowState Missing { get; } = new(false, 0, default, 96, false, false, default);
}

/// <summary>Start and dispose on the message-loop thread; changes are delivered on that thread.</summary>
public interface IGameWindow : IDisposable
{
    GameWindowState Current { get; }
    event EventHandler? Changed;
    void Start();
}
