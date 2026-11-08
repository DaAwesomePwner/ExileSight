using ExileSight.Core.Features;
using ExileSight.Core.Settings;
using ExileSight.Features.MapInfo;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.Features.MapInfo;

public static class MapInfoRegistration
{
    public static IFeature Create(IServiceProvider services) => new MapInfoFeature(new(new RatingStore(
        System.IO.Path.Combine(services.GetRequiredService<IAppPaths>().GetGameDirectory("poe1"), "mapinfo"))));
    public static void Register(IServiceProvider services, FeatureViews views) => views.RegisterManaged<MapInfoModel>(model =>
    {
        var view = new MapInfoView(services.GetRequiredService<PassiveOverlayInput>(), services.GetRequiredService<IGameWindow>(),
            services.GetRequiredService<IOverlayWindowStyler>(), services.GetRequiredService<ISettingsStore<SettingsDocument>>(),
            services.GetRequiredService<ILogger<MapInfoView>>());
        view.Start(model);
        System.Windows.Application.Current.Exit += (_, _) => view.Dispose();
    });
}
