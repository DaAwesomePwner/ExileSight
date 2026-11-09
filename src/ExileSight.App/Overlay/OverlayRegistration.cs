using ExileSight.Core.Diagnostics;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Overlay;

public static class OverlayRegistration
{
    public static IServiceCollection AddOverlays(this IServiceCollection services)
    {
        services.AddSingleton<IGameWindow, GameWindowTracker>();
        services.AddSingleton<IScreenSampler, ScreenSampler>();
        services.AddSingleton<IOverlayWindowStyler, OverlayWindowStyler>();
        services.AddSingleton<IProcessStats, OwnProcessStats>();
        services.AddSingleton<OverlayHost>();
        services.AddSingleton<OverlayController>();
        services.AddSingleton<IDebugSection>(sp => sp.GetRequiredService<OverlayController>());
        return services;
    }

    public static void StartOverlays(this IServiceProvider services) =>
        services.GetRequiredService<OverlayController>().Start();
}
