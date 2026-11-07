using ExileSight.Core.Diagnostics;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Updates;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Updates;

public static class UpdateRegistration
{
    public static IServiceCollection AddUpdates(this IServiceCollection services)
    {
        services.AddSingleton(_ => new VelopackUpdateService(
            () => UpdateSettingsSnapshot.Read(new AppPaths())));
        services.AddSingleton<IUpdateService>(sp => sp.GetRequiredService<VelopackUpdateService>());
        services.AddSingleton<IDebugSection>(sp => sp.GetRequiredService<VelopackUpdateService>());
        services.AddHostedService<UpdateHostedService>();
        return services;
    }

}
