namespace ExileSight.Platform.Abstractions.Window;

/// <summary>Operations reject windows not owned by this process.</summary>
public interface IOverlayWindowStyler
{
    void Apply(nint handle);
    void SetClickThrough(nint handle, bool enabled);
    void Position(nint handle, PixelRect rect);
}

public readonly record struct ProcessStats(long WorkingSetBytes, long PrivateBytes, double CpuPercent);
public interface IProcessStats
{
    ProcessStats Sample();
}
