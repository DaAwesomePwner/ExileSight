using ExileSight.Core.Features;
using ExileSight.Features.MapTracker;
using ExileSight.Platform.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;
namespace ExileSight.App.Features.MapTracker;

public static class TrackerRegistration
{
    public static IFeature Create(IServiceProvider services) => new MapTrackerFeature(new TrackerModel(new RunStore(
        System.IO.Path.Combine(services.GetRequiredService<IAppPaths>().GetGameDirectory("poe1"), "maptracker"))));
    public static void Register(IServiceProvider services, FeatureViews views) =>
        views.RegisterManaged<TrackerModel>(model => model.OpenRequested += () =>
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => new TrackerWindow(model).Show()));
}
