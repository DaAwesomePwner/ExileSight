using System.IO;
using ExileSight.Core.DataPacks.Loading;
using ExileSight.Core.Diagnostics;
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
        services.AddSingleton<GitHubTransport>(_ => new("1.0.0"));
        services.AddSingleton(sp =>
        {
            var transport = sp.GetRequiredService<GitHubTransport>();
            var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExileSight", "cache", "poe1", "packs");
            var fallback = Path.Combine(AppContext.BaseDirectory, "packs", "poe1");
            var loader = new PackLoader(cache, transport, fallback);
            var cacheOnly = new PackLoader(cache, transport);
            return new PackStartup(async (id, token) =>
            {
                var cached = await cacheOnly.ReadLocalAsync(id, token).ConfigureAwait(false);
                if (cached.Pack is not null) return (cached.Pack, "cache");
                var local = await loader.ReadLocalAsync(id, token).ConfigureAwait(false);
                return (local.Pack, local.Pack is null ? "none" : "fallback");
            }, async (id, token) => { await loader.RefreshAsync(id, releaseTag, token).ConfigureAwait(false); });
        });
        services.AddSingleton<IDebugSection>(sp => sp.GetRequiredService<PackStartup>());
        services.AddHostedService<PackRefreshService>();
        return services;
    }
}
public sealed class PackRefreshService(PackStartup packs, ILogger<PackRefreshService> logger) : BackgroundService
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
                try { await packs.RefreshAsync(stoppingToken).ConfigureAwait(false); }
                catch (Exception error) when (error is not OperationCanceledException)
                { packs.ReportFailure("Refresh", error); logger.LogWarning("Pack refresh failed: {ExceptionType}", error.GetType().Name); }
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
