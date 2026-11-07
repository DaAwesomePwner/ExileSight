namespace ExileSight.Core.Input;

public static class ForegroundRule
{
    public static bool Matches(string windowClass, uint pid, string title, IReadOnlySet<uint> geforceIds) =>
        windowClass == "POEWindowClass" ||
        (geforceIds.Contains(pid) && title.Contains("Path of Exile", StringComparison.OrdinalIgnoreCase));
}
