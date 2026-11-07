using ExileSight.Core.Features;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Macros;

/// <summary>Only physical release dispatches. Holds and missing releases never send input.</summary>
public sealed class MacroPress(IFeatureContext context, MacroWheel wheel, bool travel)
{
    public const long LongPressMilliseconds = 200;
    private long lastId = -1, version;
    private PressStarted? pending;
    private IntentToken? token;
    public ActionResult Start(IntentToken? intent, PressStarted press)
    {
        if (press.Id <= lastId) return new(ActionOutcome.UsedOrUnknownToken);
        lastId = press.Id;
        pending = null;
        token = null;
        version = wheel.Reset();
        if (intent is null) return new(ActionOutcome.MissingToken);
        pending = press;
        token = intent;
        return new(ActionOutcome.Success);
    }
    public void Release(PressReleased release)
    {
        if (pending is not { } down || down.Id != release.Id || down.Timestamp != release.DownTimestamp) return;
        var intent = token;
        pending = null;
        token = null;
        if (release.UpTimestamp < down.Timestamp) return;
        var settings = wheel.ReadSettings();
        if (release.UpTimestamp - down.Timestamp > LongPressMilliseconds)
        {
            var id = version;
            var commands = travel ? settings.Destinations : settings.Commands;
            context.DispatchFeature(() => wheel.Show(id, commands));
        }
        else
        {
            var command = travel ? "/hideout" : settings.Commands[0];
            if (!string.IsNullOrWhiteSpace(command))
                context.Execute(intent, new GameAction.SendChatCommand(command));
        }
    }
}
