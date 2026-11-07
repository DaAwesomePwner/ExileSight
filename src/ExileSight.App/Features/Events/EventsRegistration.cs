using ExileSight.App.Overlay;
using ExileSight.Features.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.Features.Events;

public static class EventsRegistration
{
    public static void Register(IServiceProvider services, FeatureViews views) =>
        views.RegisterManaged<EventsModel>(model => _ = new EventsView(model, services.GetRequiredService<OverlayHost>(),
            services.GetRequiredService<ILogger<EventsView>>()));
}
