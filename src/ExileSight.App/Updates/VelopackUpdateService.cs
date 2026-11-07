using System.Reflection;
using ExileSight.Core.Diagnostics;
using ExileSight.Core.Settings;
using ExileSight.Core.Updates;
using ExileSight.Platform.Abstractions.Updates;
using Velopack;
using Velopack.Sources;

namespace ExileSight.App.Updates;

public sealed class VelopackUpdateService(Func<GeneralSettings> readSettings) : IUpdateService, IDebugSection
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

    public string CurrentVersion { get; } = typeof(App).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";
    public string Title => "Updates";

    public async Task<UpdateStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        await operation.WaitAsync(CancellationToken.None);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var settings = readSettings();
            var candidate = CreateManager(settings);
            if (UpdatePolicy.UnavailableReason(settings, candidate.IsInstalled) is { } reason)
                return Set(new UpdateStatus.Unavailable(reason));
            if (!UpdatePolicy.IsDue(settings, started, lastCheck, DateTimeOffset.UtcNow)) return ReadStatus();
            lock (sync) lastCheck = DateTimeOffset.UtcNow; // Failed attempts are rate-limited too.
            var update = await candidate.CheckForUpdatesAsync();
            cancellationToken.ThrowIfCancellationRequested();
            // A later offline check must not discard an already downloaded update.
            if (update is null) return Set(new UpdateStatus.UpToDate());
            downloaded = downloaded && channel == settings.UpdateChannel &&
                available?.TargetFullRelease.Version == update.TargetFullRelease.Version;
            manager = candidate;
            available = update;
            channel = settings.UpdateChannel;
            return Set(new UpdateStatus.Available(update.TargetFullRelease.Version.ToString(), Downloaded: downloaded));
        }
        catch (Exception error) { return Set(new UpdateStatus.Unavailable(error.Message)); }
        finally { operation.Release(); }
    }

    public async Task<UpdateStatus> DownloadAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        await operation.WaitAsync(CancellationToken.None);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var settings = readSettings();
            if (UpdatePolicy.UnavailableReason(settings, manager?.IsInstalled == true) is { } reason)
                return Set(new UpdateStatus.Unavailable(reason));
            if (settings.UpdateChannel != channel || available is null)
                return Set(new UpdateStatus.Unavailable("check the selected channel before downloading"));
            if (!downloaded)
                await manager!.DownloadUpdatesAsync(available, p => progress?.Report(p), cancellationToken);
            downloaded = true;
            return Set(new UpdateStatus.Available(available.TargetFullRelease.Version.ToString(), Downloaded: true));
        }
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
            var settings = readSettings();
            bool requested;
            lock (sync) requested = applyRequested;
            if (requested && downloaded && settings.CheckForUpdates && settings.UpdateChannel == channel)
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
            new("Status", status.ToString())
        ];
    }

    private static UpdateManager CreateManager(GeneralSettings settings) => new(new GithubSource(
        "https://github.com/DaAwesomePwner/ExileSight", accessToken: null,
        prerelease: UpdatePolicy.IncludePrereleases(settings), downloader: new GithubDownloader()));

    private UpdateStatus Set(UpdateStatus value) { lock (sync) return status = value; }
    private UpdateStatus ReadStatus() { lock (sync) return status; }
}
