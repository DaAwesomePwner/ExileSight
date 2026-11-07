using ExileSight.Core.Features;
using ExileSight.Core.Pricing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Shell;
using ExileSight.Platform.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.Features;

public static class FeatureRegistration
{
    // Add new compiled-in IFeature implementations here. Never discover/load plugin assemblies.
    private static IFeature[] CompiledModules(IServiceProvider services) => [new ExileSight.Features.Omni.OmniFeature(),
        new ExileSight.Features.ItemInfo.ItemInfoFeature
        {
            ReadData = () => services.GetRequiredService<DataPacks.PackStartup>().Data,
            PackStatus = () => services.GetRequiredService<DataPacks.PackStartup>().TooltipStatus,
            SectionError = error => services.GetRequiredService<ILogger<FeatureHost>>().LogWarning(
                "Tooltip section failed: {ExceptionType}", error.GetType().Name)
        },
        new ExileSight.Features.GearCmp.GearCmpFeature(new(new(
            System.IO.Path.Combine(services.GetRequiredService<IAppPaths>().GetGameDirectory("poe1"), "gearcmp"))))
        {
            MatchLine = line => services.GetRequiredService<DataPacks.PackStartup>().Data?.MatchLine(line) ?? []
        },
        new ExileSight.Features.Macros.MacrosFeature(),
        Vaalasync.ShopRegistration.Create(services),
        Stashninja.StashRegistration.Create(services)];
    public static IServiceCollection AddFeatureModules(this IServiceCollection services)
    {
        services.AddSingleton<IBrowser, WindowsBrowser>();
        services.TryAddSingleton<PriceTransport>(_ => new(
            typeof(FeatureRegistration).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"));
        services.TryAddSingleton<PriceService>();
        services.AddSingleton<FeatureHost>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsStore<SettingsDocument>>();
            return new(CompiledModules(sp),
                (id, key) => settings.Snapshot.Features.Sections.TryGetValue(id, out var section) &&
                    section.TryGetValue(key, out var value) ? value : null,
                (id, key, value) => settings.Update("features", document =>
                {
                    if (!document.Features.Sections.TryGetValue(id, out var section))
                        document.Features.Sections[id] = section = [];
                    section[key] = value.Clone();
                }), (id, token, action) => sp.GetRequiredService<IActionGate>().TryExecute(token, action, id));
        });
        services.AddSingleton<FeatureViews>();
        services.AddSingleton<OmniMenuView>();
        services.AddSingleton<PassiveOverlayInput>();
        services.AddSingleton<ItemInfo.ItemInfoTooltip>();
        services.AddHostedService<FeatureLogPump>();
        return services;
    }
    public static void StartFeatureViews(this IServiceProvider services)
    {
        var views = services.GetRequiredService<FeatureViews>();
        Vaalasync.ShopRegistration.Register(services, views);
        Stashninja.StashRegistration.Register(services, views);
        views.RegisterManaged<ExileSight.Features.ItemInfo.ItemInfoViewModel>(services.GetRequiredService<ItemInfo.ItemInfoTooltip>().Start);
        Macros.MacrosRegistration.Register(services, views);
        views.Start();
        Macros.MacrosRegistration.Initialize();
        Vaalasync.ShopRegistration.Initialize(services);
        Stashninja.StashRegistration.Initialize(services);
        var model = services.GetRequiredService<FeatureHost>().Overlays
            .Select(o => o.ViewModel).OfType<ExileSight.Features.Omni.OmniMenu>().Single();
        services.GetRequiredService<OmniMenuView>().Start(model);
    }
}
