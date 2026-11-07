namespace ExileSight.Platform.Abstractions.Window;

/// <summary>PrivateBytes is zero when unavailable through handle-free own-process APIs.</summary>
public readonly record struct ProcessStats(long WorkingSetBytes, long PrivateBytes, double CpuPercent);
