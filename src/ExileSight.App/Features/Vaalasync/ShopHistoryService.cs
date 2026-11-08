using ExileSight.Core.Features;
using ExileSight.Features.Vaalasync;
using Microsoft.Extensions.Hosting;

namespace ExileSight.App.Features.Vaalasync;

/// <summary>Drains pending shop history within the host's shutdown budget, off input/UI threads.</summary>
public sealed class ShopHistoryService(FeatureHost features) : IHostedService
{
    private readonly ShopModel model = features.Overlays.Select(o => o.ViewModel).OfType<ShopModel>().Single();

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Do not pass the stop token to Task.Run: even an expired budget must stop accepting
    // input, release discovery waits and report unfinished history.
    public Task StopAsync(CancellationToken cancellationToken) => Task.Run(() =>
        model.StopAndFlushHistoryAsync(TimeSpan.FromSeconds(2), cancellationToken));
}
