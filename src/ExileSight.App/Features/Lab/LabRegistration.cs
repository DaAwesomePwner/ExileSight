using ExileSight.Core.Settings;
using ExileSight.App.Overlay;
using ExileSight.Core.Features;
using ExileSight.Features.Lab;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Shell;
using ExileSight.Platform.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Lab;

public static class LabRegistration
{
    public static IFeature Create(IServiceProvider services) => new LabFeature(new LabModel(new LabStore(
        System.IO.Path.Combine(services.GetRequiredService<IAppPaths>().GetGameDirectory("poe1"), "lab"))));
    public static void Register(IServiceProvider services, FeatureViews views) => views.RegisterManaged<LabModel>(model =>
        _ = new LabView(model, services.GetRequiredService<OverlayHost>(),
            services.GetRequiredService<IBrowser>(), services.GetRequiredService<IClipboard>(), services.GetRequiredService<ISettingsStore<SettingsDocument>>()));
}
