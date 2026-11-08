using ExileSight.Core.Settings;

namespace ExileSight.Core.Updates;

public static class UpdatePolicy
{
    public static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    public static bool ChannelChanged(string? packageChannel, string currentChannel) =>
        packageChannel is not null && packageChannel != currentChannel;

    public static bool ShouldApply(bool requested, bool downloaded, bool enabled, string? packageChannel, string currentChannel) =>
        requested && downloaded && enabled && packageChannel == currentChannel;

    public static bool KeepDownload(bool downloaded, string? packageChannel, string currentChannel,
        string? packageVersion, string? checkedVersion) =>
        downloaded && packageChannel == currentChannel && checkedVersion is not null && packageVersion == checkedVersion;

    public const string RepositoryUrl = "https://github.com/DaAwesomePwner/ExileSight";

    // The persisted/user-facing "prerelease" setting means the Velopack beta channel.
    // Keep this mapping separate from settings validation: no migration or write is needed.
    public static string ReleaseChannel(GeneralSettings settings) =>
        string.Equals(settings.UpdateChannel, "prerelease", StringComparison.OrdinalIgnoreCase) ? "beta" : "stable";

    public static bool IncludePrereleases(GeneralSettings settings) => ReleaseChannel(settings) == "beta";

    public static string? UnavailableReason(GeneralSettings settings, bool installed) =>
        !installed ? "not installed via Velopack" : !settings.CheckForUpdates ? "update checks disabled" : null;

    public static bool IsDue(GeneralSettings settings, DateTimeOffset started, DateTimeOffset? lastAttempt, DateTimeOffset now) =>
        settings.CheckForUpdates && now >= started + StartupDelay &&
        (lastAttempt is null || now >= lastAttempt.Value + CheckInterval);
}
