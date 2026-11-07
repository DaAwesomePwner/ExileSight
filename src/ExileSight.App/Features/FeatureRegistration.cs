using ExileSight.Core.Features;
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
    private static IFeature[] CompiledModules(IServiceProvider services) => [];
    public static IServiceCollection AddFeatureModules(this IServiceCollection services)
    {
        services.AddSingleton<IBrowser, WindowsBrowser>();
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
        services.AddHostedService<FeatureLogPump>();
        return services;
    }
    public static void StartFeatureViews(this IServiceProvider services) =>
        services.GetRequiredService<FeatureViews>().Start();
}
