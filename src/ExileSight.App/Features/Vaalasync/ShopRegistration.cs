using System.IO;
using Microsoft.Extensions.Logging;
using ExileSight.Core.Features;
using ExileSight.Core.Settings;
using ExileSight.Features.Vaalasync;
using ExileSight.Platform.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Vaalasync;

public static class ShopRegistration
{
    public static IFeature Create(IServiceProvider services)
    {
        var paths = services.GetRequiredService<IAppPaths>();
        var settings = services.GetRequiredService<ISettingsStore<SettingsDocument>>();
        return new VaalasyncFeature(new ShopModel(new HistoryStore(Path.Combine(paths.GetGameDirectory("poe1"), "vaalasync")),
            () => string.IsNullOrWhiteSpace(settings.Snapshot.Prices.League) ? "Standard" : settings.Snapshot.Prices.League)
        {
            ReportFailure = type => services.GetRequiredService<ILogger<ShopModel>>().LogWarning("Shop executor failed: {ExceptionType}", type)
        });
    }
    private static ShopView? view;
    public static void Register(IServiceProvider services, FeatureViews views) => views.Register<ShopModel>(model =>
        view = new ShopView(model, views, services));
    public static void Initialize(IServiceProvider services) => view?.Initialize();
}
