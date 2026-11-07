using System.IO;
using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Features.Stashninja;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Stashninja;

public static class StashRegistration
{
    public static IFeature Create(IServiceProvider services)
    {
        Layout[] layouts = []; string? error = null;
        try { layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data", "stash-layouts", "poe1")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { error = "Layout files unavailable or invalid. See data/stash-layouts README."; }
        return new StashninjaFeature(new StashModel { Layouts = layouts, LayoutError = error });
    }
    private static StashView? view;
    public static void Register(IServiceProvider services, FeatureViews views) =>
        views.Register<StashModel>(model => view = new StashView(model, services, views));
    public static void Initialize(IServiceProvider services) => view?.Initialize();
}
