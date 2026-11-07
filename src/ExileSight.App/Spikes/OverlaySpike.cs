using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Spikes;

public static class OverlaySpike
{
    public static IServiceCollection AddOverlaySpike(this IServiceCollection s) => s;

    public static void StartOverlaySpike(IServiceProvider sp) { }
}
