using ExileSight.App.Overlay;
using ExileSight.Features.SearchStrings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.Features.SearchStrings;

public static class SearchStringsRegistration
{
    private static SearchStringsView? view;
    public static void Register(IServiceProvider services, FeatureViews views) =>
        views.Register<SearchMenu>(model => view = new(model, views,
            services.GetRequiredService<OverlayHost>(), services.GetRequiredService<PassiveOverlayInput>(),
            services.GetRequiredService<ILogger<SearchStringsView>>()));
    public static void Initialize() => view?.Initialize();
}
