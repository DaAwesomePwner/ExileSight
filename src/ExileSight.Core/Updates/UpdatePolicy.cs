using ExileSight.Core.Settings;

namespace ExileSight.Core.Updates;

public static class UpdatePolicy
{
    public static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    public static bool IncludePrereleases(GeneralSettings settings) => settings.UpdateChannel == "prerelease";

    public static string? UnavailableReason(GeneralSettings settings, bool installed) =>
        !installed ? "not installed via Velopack" : !settings.CheckForUpdates ? "update checks disabled" : null;

    public static bool IsDue(GeneralSettings settings, DateTimeOffset started, DateTimeOffset? lastAttempt, DateTimeOffset now) =>
        settings.CheckForUpdates && now >= started + StartupDelay &&
        (lastAttempt is null || now >= lastAttempt.Value + CheckInterval);
}
