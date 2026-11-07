using ExileSight.Core.Diagnostics;
using ExileSight.Core.Games;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Updates;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Updates;

public static class UpdateRegistration
{
    public static IServiceCollection AddUpdates(this IServiceCollection services)
    {
        // Lazy, background-only settings access. Replace this reader with the shared active-profile
        // snapshot when the settings UI owns the profile stores; do not open duplicate store owners.
        services.AddSingleton(_ => new VelopackUpdateService(ReadSettings));
        services.AddSingleton<IUpdateService>(sp => sp.GetRequiredService<VelopackUpdateService>());
        services.AddSingleton<IDebugSection>(sp => sp.GetRequiredService<VelopackUpdateService>());
        services.AddHostedService<UpdateHostedService>();
        return services;
    }

    private static GeneralSettings ReadSettings()
    {
        using var profiles = new ProfileStore(new AppPaths(), Game.PoE1);
        using var settings = profiles.Open(profiles.ActiveProfileId);
        return settings.Snapshot.General;
    }
}
