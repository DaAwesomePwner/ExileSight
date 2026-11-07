using System.IO;
using System.Runtime;
using System.Text.Json;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Windows.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.Diagnostics;

/// <summary>Opt-in own-process observations only. Does not collect GC, alter startup, or disable services.</summary>
public sealed class MemoryReportService(IAppPaths paths, ILogger<MemoryReportService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (Environment.GetEnvironmentVariable("EXILESIGHT_PERF_MEMORY_REPORT") != "1") return;
        var path = Path.Combine(paths.RootDirectory, "perf-memory.json");
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var resources = OwnProcessResources.Read();
                    var gc = GC.GetGCMemoryInfo();
                    var json = JsonSerializer.Serialize(new
                    {
                        utc = DateTimeOffset.UtcNow, resources.PrivateBytes,
                        managedBytes = GC.GetTotalMemory(false), gc.HeapSizeBytes,
                        gc.TotalCommittedBytes, gc.FragmentedBytes, GCSettings.IsServerGC,
                        gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2)
                    });
                    Directory.CreateDirectory(paths.RootDirectory);
                    await File.WriteAllTextAsync(path + ".tmp", json, stoppingToken).ConfigureAwait(false);
                    File.Move(path + ".tmp", path, overwrite: true);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { logger.LogWarning("Memory report unavailable: {ExceptionType}", error.GetType().Name); }
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
