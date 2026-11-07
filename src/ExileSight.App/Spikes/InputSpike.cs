using ExileSight.App.Input;
using ExileSight.Core.Diagnostics;
using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Windows.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Spikes;

public static class InputSpike
{
    public static IServiceCollection AddInputSpike(this IServiceCollection s)
    {
        // Phase 0 stand-in manifest, bound to this gate; features cannot supply their own action set.
        var clock = new SystemClock();
        var registry = new IntentRegistry(clock);
        var policy = new GatePolicy(registry, [ActionKind.CopyItem, ActionKind.SendChatCommand]);
        var dispatcher = new HotkeyDispatcher(registry, [
            new("spike.copy", 0x76, KeyModifiers.None, true, new GameAction.CopyItem()),
            new("spike.hideout", 0x77, KeyModifiers.None, true, new GameAction.SendChatCommand("/hideout"))
        ]);
        s.AddSingleton<IClock>(clock);
        s.AddSingleton<IClipboard, WindowsClipboard>();
        s.AddSingleton<IActionGate>(sp => new ActionGate(policy, sp.GetRequiredService<IClipboard>(), clock));
        s.AddSingleton<InputDiagnostics>();
        s.AddSingleton<IDebugSection>(sp => sp.GetRequiredService<InputDiagnostics>());
        s.AddSingleton<IHotkeys>(sp => new HookHost(dispatcher, clock, sp.GetRequiredService<InputDiagnostics>().Execute));
        return s;
    }
    public static void StartInputSpike(IServiceProvider sp) =>
        sp.GetRequiredService<InputDiagnostics>().Start(sp.GetRequiredService<IHotkeys>());
}
