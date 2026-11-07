using ExileSight.Core.Features;
using ExileSight.Core.Pricing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Shell;
using ExileSight.Platform.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features;

public static class FeatureRegistration
{
    // Add new compiled-in IFeature implementations here. Never discover/load plugin assemblies.
    private static IFeature[] CompiledModules(IServiceProvider services) => [new ExileSight.Features.Omni.OmniFeature(), Vaalasync.ShopRegistration.Create(services)];
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
        services.AddHostedService<FeatureLogPump>();
        return services;
    }
    public static void StartFeatureViews(this IServiceProvider services)
    {
        var views = services.GetRequiredService<FeatureViews>();
        Vaalasync.ShopRegistration.Register(services, views);
        views.Start();
        Vaalasync.ShopRegistration.Initialize(services);
        var model = services.GetRequiredService<FeatureHost>().Overlays
            .Select(o => o.ViewModel).OfType<ExileSight.Features.Omni.OmniMenu>().Single();
        services.GetRequiredService<OmniMenuView>().Start(model);
    }
}
