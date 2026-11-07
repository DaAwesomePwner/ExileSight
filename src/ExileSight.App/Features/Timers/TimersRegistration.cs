using ExileSight.App.Overlay;
using ExileSight.Core.Features;
using ExileSight.Features.Timers;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Timers;

public static class TimersRegistration
{
    public static IFeature Create(IServiceProvider services) => new TimersFeature(new TimersModel(
        new TimerStore(System.IO.Path.Combine(services.GetRequiredService<IAppPaths>().GetGameDirectory("poe1"), "timers")),
        services.GetRequiredService<IClock>()));
    public static void Register(IServiceProvider services, FeatureViews views) =>
        views.RegisterManaged<TimersModel>(model => _ = new TimersView(model,
            services.GetRequiredService<OverlayHost>(), services.GetRequiredService<IGameWindow>()));
}
