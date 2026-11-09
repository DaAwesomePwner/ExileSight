using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.App.Overlay;
using ExileSight.Core.Features;
using ExileSight.Platform.Windows.Input;
using ExileSight.Features.SearchStrings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.Features.SearchStrings;

public static class SearchStringsRegistration
{
    private static SearchStringsView? view;
    public static void Register(IServiceProvider services, FeatureViews views) =>
        views.Register<SearchMenu>(model => view = new(model, services.GetRequiredService<FeatureHost>(), services.GetRequiredService<HookHost>(),
            services.GetRequiredService<OverlayHost>(), services.GetRequiredService<PassiveOverlayInput>(),
            services.GetRequiredService<ILogger<SearchStringsView>>(), services.GetRequiredService<ISettingsStore<SettingsDocument>>()));
    public static void Initialize() => view?.Initialize();
}
