using ExileSight.App.Overlay;
using ExileSight.Core.Diagnostics;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Spikes;

public static class OverlaySpike
{
    public static IServiceCollection AddOverlaySpike(this IServiceCollection services)
    {
        services.AddSingleton<IGameWindow, GameWindowTracker>();
        services.AddSingleton<IOverlayWindowStyler, OverlayWindowStyler>();
        services.AddSingleton<IProcessStats, OwnProcessStats>();
        services.AddSingleton<OverlayController>();
        services.AddSingleton<IDebugSection>(sp => sp.GetRequiredService<OverlayController>());
        return services;
    }

    public static void StartOverlaySpike(IServiceProvider sp) =>
        sp.GetRequiredService<OverlayController>().Start();
}
