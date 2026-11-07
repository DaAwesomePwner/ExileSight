using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.Platform.Windows.Input;

public sealed partial class HookHost
{
    public IActionGate Gate { get; private set; } = null!;
    public static HookHost Create(IClock clock, HotkeyConfiguration configuration, IClipboard clipboard,
        IGameWindow window, Func<IntentToken?, HotkeyBinding, ActionResult> execute,
        Action<HotkeyBinding, ActionResult> report)
    {
        var issuer = new PhysicalIssuer(clock);
        var repeat = new KeyboardRepeatSettings();
        var dispatcher = new HotkeyDispatcher(issuer.Registry, configuration);
        dispatcher.InstallIssuer(issuer.CreateHookIssuer());
        return new HookHost(dispatcher, clock, execute, repeat, report, window)
        {
            Gate = new ActionGate(new GatePolicy(issuer.Registry,
                [ActionKind.CopyItem, ActionKind.SendChatCommand]), clipboard, clock)
        };
    }
    // The production registry's capability never leaves this private construction path.
    private sealed class PhysicalIssuer
    {
        private readonly object capability = new();
        internal IntentRegistry Registry { get; }
        internal PhysicalIssuer(IClock clock) => Registry = new(clock, capability);
        internal Func<long, string, ActionKind, IntentToken> CreateHookIssuer() => Issue;
        private IntentToken Issue(long timestamp, string hotkey, ActionKind action) =>
            Registry.Register(capability, timestamp, hotkey, action);
    }
}
