using ExileSight.Core.Diagnostics;
using ExileSight.Core.Games;
using ExileSight.Core.Input;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;
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
        services.AddSingleton<InputPipeline>();
        services.AddSingleton<IClipboard, WindowsClipboard>();
        services.AddSingleton<IActionGate>(sp => sp.GetRequiredService<InputPipeline>().Gate);
        services.AddSingleton<HotkeyExecution>();
        services.AddSingleton<InputDiagnostics>();
        services.AddSingleton<IDebugSection>(sp => sp.GetRequiredService<InputDiagnostics>());
        services.AddSingleton<IHotkeys>(sp => new HookHost(sp.GetRequiredService<InputPipeline>().Dispatcher,
            sp.GetRequiredService<IClock>(), sp.GetRequiredService<HotkeyExecution>().Execute,
            sp.GetRequiredService<InputPipeline>().RepeatSettings, sp.GetRequiredService<InputDiagnostics>().Report));
        return services;
    }
    public static void StartProductionInput(IServiceProvider services)
    {
        services.GetRequiredService<HotkeySettings>();
        services.GetRequiredService<InputDiagnostics>().Start(services.GetRequiredService<IHotkeys>());
    }
    // Private wiring holds authority. IntentRegistry itself is never a DI service.
    private sealed class InputPipeline
    {
        public KeyboardRepeatSettings RepeatSettings { get; } = new();
        public HotkeyDispatcher Dispatcher { get; }
        public IActionGate Gate { get; }
        public InputPipeline(IClock clock, HotkeyConfiguration configuration, IClipboard clipboard)
        {
            var registry = new IntentRegistry(clock);
            Dispatcher = new(registry, configuration, () => RepeatSettings.SilenceMilliseconds);
            Gate = new ActionGate(new GatePolicy(registry, [ActionKind.CopyItem, ActionKind.SendChatCommand]),
                clipboard, clock);
        }
    }
}
