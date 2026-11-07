using ExileSight.Features.Macros;

namespace ExileSight.App.Features.Macros;

public static class MacrosRegistration
{
    private static MacroWheelView? view;
    public static void Register(IServiceProvider services, FeatureViews views) =>
        views.Register<MacroWheel>(model => view = new(model, views, services));
    public static void Initialize() => view?.Initialize();
}
