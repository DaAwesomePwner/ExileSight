using System.Reflection;
using ExileSight.Core.Diagnostics;
using ExileSight.Core.Settings;
using ExileSight.Core.Updates;
using ExileSight.Platform.Abstractions.Updates;
using Velopack;
using Velopack.Sources;

namespace ExileSight.App.Updates;

public sealed class VelopackUpdateService(Func<UpdateSettingsSnapshot> readSettings) : IUpdateService, IDebugSection
{
    private readonly SemaphoreSlim operation = new(1);
    private readonly object sync = new();
    private readonly DateTimeOffset started = DateTimeOffset.UtcNow;
    private UpdateStatus status = new UpdateStatus.Unavailable("not checked");
    private DateTimeOffset? lastCheck;
    private UpdateManager? manager;
    private UpdateInfo? available;
    private string? channel;
    private bool downloaded;
    private bool applyRequested;
    private string settingsWarnings = "";

    public string CurrentVersion { get; } = typeof(App).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";
    public string Title => "Updates";

    public async Task<UpdateStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        await operation.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var settings = ReadSettings();
            ReconcileChannel(settings.UpdateChannel);
            var candidate = CreateManager(settings, cancellationToken);
            if (UpdatePolicy.UnavailableReason(settings, candidate.IsInstalled) is { } reason)
                return Set(new UpdateStatus.Unavailable(reason));
            if (!UpdatePolicy.IsDue(settings, started, lastCheck, DateTimeOffset.UtcNow)) return ReadStatus();
            lock (sync) lastCheck = DateTimeOffset.UtcNow; // Failed attempts are rate-limited too.
            var update = await candidate.CheckForUpdatesAsync().WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            downloaded = UpdatePolicy.KeepDownload(downloaded, channel, settings.UpdateChannel,
                available?.TargetFullRelease.Version.ToString(), update?.TargetFullRelease.Version.ToString());
            if (update is null)
            {
                ClearPending(); // A pulled release must never be applied on exit.
                return Set(new UpdateStatus.UpToDate());
            }
            if (!downloaded) { lock (sync) applyRequested = false; }
            manager = candidate;
            available = update;
            channel = settings.UpdateChannel;
            return Set(new UpdateStatus.Available(update.TargetFullRelease.Version.ToString(), Downloaded: downloaded));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) { return Set(new UpdateStatus.Unavailable(error.Message)); }
        finally { operation.Release(); }
    }

    public async Task<UpdateStatus> DownloadAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        await operation.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var settings = ReadSettings();
            ReconcileChannel(settings.UpdateChannel);
            if (UpdatePolicy.UnavailableReason(settings, manager?.IsInstalled == true) is { } reason)
                return Set(new UpdateStatus.Unavailable(reason));
            if (settings.UpdateChannel != channel || available is null)
                return Set(new UpdateStatus.Unavailable("check the selected channel before downloading"));
            if (!downloaded)
            {
                var candidate = CreateManager(settings, cancellationToken);
                await candidate.DownloadUpdatesAsync(available, p => progress?.Report(p), cancellationToken)
                    .WaitAsync(cancellationToken);
                manager = candidate;
            }
            cancellationToken.ThrowIfCancellationRequested();
            downloaded = true;
            return Set(new UpdateStatus.Available(available.TargetFullRelease.Version.ToString(), Downloaded: true));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) { return Set(new UpdateStatus.Unavailable(error.Message)); }
        finally { operation.Release(); }
    }

    public void ApplyOnRestart()
    {
        lock (sync) applyRequested = true;
    }

    // Called only after the hosted worker has stopped, on normal application shutdown.
    public void ApplyAfterExit()
    {
        if (!operation.Wait(0)) return;
        try
        {
            var settings = ReadSettings();
            ReconcileChannel(settings.UpdateChannel);
            bool requested;
            lock (sync) requested = applyRequested;
            if (UpdatePolicy.ShouldApply(requested, downloaded, settings.CheckForUpdates, channel, settings.UpdateChannel))
                manager?.WaitExitThenApplyUpdates(available!.TargetFullRelease, silent: true, restart: false);
        }
        catch (Exception error) { Set(new UpdateStatus.Unavailable(error.Message)); }
        finally { operation.Release(); }
    }

    public IReadOnlyList<KeyValuePair<string, string>> Snapshot()
    {
        lock (sync) return [
            new("Current version", CurrentVersion),
            new("Last check / UTC", lastCheck?.ToString("O") ?? "never"),
            new("Status", status.ToString()),
            new("Settings warnings", settingsWarnings)
        ];
    }

    private static UpdateManager CreateManager(GeneralSettings settings, CancellationToken token) => new(new GithubSource(
        UpdatePolicy.RepositoryUrl, accessToken: null,
        prerelease: UpdatePolicy.IncludePrereleases(settings), downloader: new GithubDownloader(token)),
        new UpdateOptions { ExplicitChannel = UpdatePolicy.ReleaseChannel(settings), AllowVersionDowngrade = false });

    private GeneralSettings ReadSettings()
    {
        var snapshot = readSettings();
        lock (sync) settingsWarnings = string.Join("; ", snapshot.Warnings);
        return snapshot.Settings;
    }

    private void ReconcileChannel(string currentChannel)
    {
        if (!UpdatePolicy.ChannelChanged(channel, currentChannel)) return;
        ClearPending();
        Set(new UpdateStatus.Unavailable("channel changed; awaiting next scheduled check"));
    }

    private void ClearPending()
    {
        downloaded = false;
        available = null;
        manager = null;
        channel = null;
        lock (sync) applyRequested = false;
    }

    private UpdateStatus Set(UpdateStatus value) { lock (sync) return status = value; }
    private UpdateStatus ReadStatus() { lock (sync) return status; }
}
