using ExileSight.Core.Diagnostics;
using ExileSight.Core.Games;
using ExileSight.Core.Input;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExileSight.App.Input;

public static class InputRegistration
{
    public static IServiceCollection AddProductionInput(this IServiceCollection services)
    {
        services.TryAddSingleton<IAppPaths, AppPaths>();
        services.TryAddSingleton<ProfileStore>(sp => new ProfileStore(sp.GetRequiredService<IAppPaths>(), Game.PoE1));
        services.TryAddSingleton<IProfileStore>(sp => sp.GetRequiredService<ProfileStore>());
        services.TryAddSingleton<ISettingsStore<SettingsDocument>>(sp =>
        {
            var profiles = sp.GetRequiredService<ProfileStore>();
            return profiles.Open(profiles.ActiveProfileId);
        });
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton<HotkeyConfiguration>();
        services.AddSingleton<IHotkeyContexts>(sp => sp.GetRequiredService<HotkeyConfiguration>());
        services.AddSingleton<HotkeySettings>(sp => new(
            () => sp.GetRequiredService<ISettingsStore<SettingsDocument>>(),
            sp.GetRequiredService<HotkeyConfiguration>(), sp.GetServices<IFeatureCommandHandler>()));
        services.AddSingleton<IClipboard, WindowsClipboard>();
        services.AddSingleton<HookHost>(sp => HookHost.Create(sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<HotkeyConfiguration>(), sp.GetRequiredService<IClipboard>(),
            sp.GetRequiredService<IGameWindow>(),
            (token, binding) => sp.GetRequiredService<HotkeyExecution>().Execute(token, binding),
            (binding, result) => sp.GetRequiredService<InputDiagnostics>().Report(binding, result)));
        services.AddSingleton<IActionGate>(sp => sp.GetRequiredService<HookHost>().Gate);
        services.AddSingleton<HotkeyExecution>();
        services.AddSingleton<InputDiagnostics>();
        services.AddSingleton<IDebugSection>(sp => sp.GetRequiredService<InputDiagnostics>());
        services.AddSingleton<IHotkeys>(sp => sp.GetRequiredService<HookHost>());
        return services;
    }
    public static void StartProductionInput(IServiceProvider services)
    {
        services.GetRequiredService<HotkeySettings>();
        services.GetRequiredService<InputDiagnostics>().Start(services.GetRequiredService<IHotkeys>());
    }
}
