using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Macros;

[DeclaredActions(ActionKind.SendChatCommand)]
public sealed class MacrosFeature : IFeature
{
    public FeatureManifest Manifest { get; } = new("macros", [Game.PoE1], [ActionKind.SendChatCommand],
        [], [], "F5: hideout / travel wheel; F6: command 0 / eight-slot macro wheel. One chat command per gesture.");
    public MacroWheel Wheel { get; } = new();
    public void Register(IFeatureContext context)
    {
        Wheel.SelectSlot = (slot, version, token) => Wheel.Select(slot, token, context, version);
        Wheel.ReadSettings = () => MacroSettings.Read(context.ReadSetting("commands"));
        Wheel.SaveSettings = settings =>
        {
            if (!settings.IsValid) throw new ArgumentException("Invalid macro settings");
            context.WriteSetting("commands", JsonSerializer.SerializeToElement(settings));
            Wheel.Reset();
        };
        RegisterPress("travel", 0x74, true);
        RegisterPress("macro", 0x75, false);
        for (var i = 0; i < 8; i++)
        {
            var slot = i;
            context.RegisterCommand("slot" + i, ActionKind.SendChatCommand, token => Wheel.Select(slot, token, context));
        }
        context.RegisterOverlay(Wheel);
        void RegisterPress(string name, int key, bool travel)
        {
            var press = new MacroPress(context, Wheel, travel);
            context.RegisterPressCommand(name, ActionKind.SendChatCommand, press.Start, press.Release);
            context.RegisterDefaultBinding(new("macros." + name, key, KeyModifiers.None, true, "macros:" + name));
        }
    }
}
