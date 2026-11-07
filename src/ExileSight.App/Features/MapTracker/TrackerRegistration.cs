using ExileSight.Core.Features;
using ExileSight.Features.MapTracker;
using ExileSight.Platform.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ExileSight.Platform.Abstractions.Time;
namespace ExileSight.App.Features.MapTracker;

public static class TrackerRegistration
{
    public static IFeature Create(IServiceProvider services) => new MapTrackerFeature(new TrackerModel(new RunStore(
        System.IO.Path.Combine(services.GetRequiredService<IAppPaths>().GetGameDirectory("poe1"), "maptracker")),
        services.GetRequiredService<IClock>(),
        type => services.GetRequiredService<ILogger<TrackerModel>>().LogWarning("Map tracker persistence failed: {ExceptionType}", type)));
    public static void Register(IServiceProvider services, FeatureViews views) =>
        views.RegisterManaged<TrackerModel>(model =>
        {
            TrackerWindow? window = null;
            model.OpenRequested += () => System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (window is null)
                {
                    window = new TrackerWindow(model);
                    window.Closed += (_, _) => window = null;
                    window.Show();
                }
                else
                {
                    if (window.WindowState == System.Windows.WindowState.Minimized)
                        window.WindowState = System.Windows.WindowState.Normal;
                    window.Activate();
                }
            });
            System.Windows.Application.Current.Exit += (_, _) => model.StopAsync().GetAwaiter().GetResult();
        });
}
