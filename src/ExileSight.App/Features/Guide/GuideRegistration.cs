using ExileSight.App.Overlay;
using ExileSight.Core.Features;
using ExileSight.Features.Guide;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Guide;

public static class GuideRegistration
{
    public static IFeature Create(IServiceProvider services) => new GuideFeature(new GuideModel(
        new GuideStore(System.IO.Path.Combine(services.GetRequiredService<IAppPaths>().GetGameDirectory("poe1"), "guide")),
        services.GetRequiredService<IClock>()));
    public static void Register(IServiceProvider services, FeatureViews views) => views.RegisterManaged<GuideModel>(model =>
        _ = new GuideView(model, services.GetRequiredService<OverlayHost>(), services.GetRequiredService<IGameWindow>()));
}
