namespace ExileSight.Platform.Abstractions.Window;

public sealed record GameWindowState(bool Found, nint Handle, PixelRect ClientRect,
    uint Dpi, bool IsForeground, bool IsMinimized, DateTimeOffset LastEventTime)
{
    public static GameWindowState Missing { get; } = new(false, 0, default, 96, false, false, default);
}
