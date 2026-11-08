using System.IO;
using Microsoft.Extensions.Hosting;
using ExileSight.Core.Pricing;
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
        var prices = services.GetRequiredService<PriceService>();
        var lifetime = services.GetRequiredService<IHostApplicationLifetime>();
        var league = new ShopLeague(() => settings.Snapshot.Prices.League,
            () => prices.GetLeaguesAsync(cancellationToken: lifetime.ApplicationStopping));
        var model = new ShopModel(new HistoryStore(Path.Combine(paths.GetGameDirectory("poe1"), "vaalasync")),
            () => league.Current)
        {
            ResolveLeagueAsync = league.ResolveAsync,
            SeedPrice = (item, effectiveLeague) => ListingPrices.FindAsync(prices, effectiveLeague, item),
            ReportFailure = diagnostic => services.GetRequiredService<ILogger<ShopModel>>().LogWarning("Shop diagnostic: {Diagnostic}", diagnostic)
        };
        return new VaalasyncFeature(model);
    }
    private static ShopView? view;
    public static void Register(IServiceProvider services, FeatureViews views) => views.Register<ShopModel>(model =>
        view = new ShopView(model, views, services));
    public static void Initialize(IServiceProvider services) => view?.Initialize();
}
