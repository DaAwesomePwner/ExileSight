using ExileSight.App.Overlay;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Spikes;

public static class OverlaySpike
{
    public static IServiceCollection AddOverlaySpike(this IServiceCollection services) => services.AddOverlays();
    public static void StartOverlaySpike(IServiceProvider sp) => sp.StartOverlays();
}
