using System.IO;
using ExileSight.Core.DataPacks.Loading;
using ExileSight.Core.Diagnostics;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.DataPacks;

public static class PackRegistration
{
    public const string DefaultReleaseTag = "poe1-packs-1.0.0";
    public static IServiceCollection AddItemPacks(this IServiceCollection services, string releaseTag = DefaultReleaseTag)
    {
        PackLoader.ValidateSegment(releaseTag);
        services.AddSingleton<GitHubTransport>(_ => new(
            typeof(PackRegistration).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"));
        services.AddSingleton(sp =>
        {
            var transport = sp.GetRequiredService<GitHubTransport>();
            var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExileSight", "cache", "poe1", "packs");
            var fallback = Path.Combine(AppContext.BaseDirectory, "packs", "poe1");
            var loader = new PackLoader(cache, transport, fallback);
            var cacheOnly = new PackLoader(cache, transport);
            var throttle = new PackRefreshThrottle(cache);
            var settings = sp.GetRequiredService<ISettingsStore<SettingsDocument>>();
            return new PackStartup(async (id, token) =>
            {
                var cached = await cacheOnly.ReadLocalAsync(id, token).ConfigureAwait(false);
                if (cached.Pack is not null) return (cached.Pack, "cache");
                var local = await loader.ReadLocalAsync(id, token).ConfigureAwait(false);
                return (local.Pack, local.Pack is null ? "none" : "fallback");
            }, async (id, token) =>
            {
                await throttle.RunAsync(id, releaseTag, () => settings.Snapshot.General.FirstRunCompleted,
                    async cancellation =>
                    {
                        var result = await loader.RefreshAsync(id, releaseTag, cancellation).ConfigureAwait(false);
                        return result.Pack is null ? "Checked; packs unavailable" :
                            "Checked; local version " + result.Pack.Manifest.PackVersion + " available (download outcome unknown)";
                    }, token).ConfigureAwait(false);
            }, (id, error) => sp.GetRequiredService<ILogger<PackStartup>>().LogWarning(
                "Pack {PackId} refresh failed: {ExceptionType}", id, error.GetType().Name));
        });
        services.AddSingleton<IDebugSection>(sp => sp.GetRequiredService<PackStartup>());
        services.AddHostedService<PackRefreshService>();
        return services;
    }
}
public sealed class PackRefreshService(PackStartup packs, ILogger<PackRefreshService> logger,
    ISettingsStore<SettingsDocument> settings) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            try { await packs.LoadAsync("Not checked", stoppingToken).ConfigureAwait(false); }
            catch (Exception error) when (error is not OperationCanceledException)
            { packs.ReportFailure("Local preparation", error); logger.LogWarning("Local pack preparation failed: {ExceptionType}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            while (!stoppingToken.IsCancellationRequested)
            {
                while (!settings.Snapshot.General.FirstRunCompleted)
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                try { await packs.RefreshAsync(stoppingToken).ConfigureAwait(false); }
                catch (Exception error) when (error is not OperationCanceledException)
                { packs.ReportFailure("Refresh", error); logger.LogWarning("Pack refresh failed: {ExceptionType}", error.GetType().Name); }
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
