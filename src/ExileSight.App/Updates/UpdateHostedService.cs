using ExileSight.Core.Updates;
using ExileSight.Platform.Abstractions.Updates;
using Microsoft.Extensions.Hosting;

namespace ExileSight.App.Updates;

public sealed class UpdateHostedService(VelopackUpdateService updates) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(UpdatePolicy.StartupDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await updates.CheckAsync(stoppingToken) is UpdateStatus.Available)
                {
                    if (await updates.DownloadAsync(cancellationToken: stoppingToken) is UpdateStatus.Available { Downloaded: true })
                        updates.ApplyOnRestart();
                }
                await Task.Delay(UpdatePolicy.CheckInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        updates.ApplyAfterExit();
    }
}
