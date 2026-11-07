namespace ExileSight.Platform.Abstractions.Window;

public readonly record struct ProcessStats(long WorkingSetBytes, long PrivateBytes, double CpuPercent);
