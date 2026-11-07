using ExileSight.Core.Features;
using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Macros;

/// <summary>Only physical release dispatches. Holds and missing releases never send input.</summary>
public sealed class MacroPress(IFeatureContext context, MacroWheel wheel, bool travel)
{
    public const long LongPressMilliseconds = 200;
    private long lastId = -1, version;
    private PressStarted? pending;
    private IntentToken? token;
    private Action<ActionResult>? complete;
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
        ActionResult? result = null;
        complete = value => result = value;
        return PressRouter.DeferUntilRelease(() => result);
    }
    public void Release(PressReleased release)
    {
        if (pending is not { } down || down.Id != release.Id || down.Timestamp != release.DownTimestamp) return;
        var intent = token;
        var report = complete;
        complete = null;
        pending = null;
        token = null;
        if (release.UpTimestamp < down.Timestamp) return;
        var settings = wheel.ReadSettings();
        if (release.UpTimestamp - down.Timestamp > LongPressMilliseconds)
        {
            var id = version;
            var commands = travel ? settings.Destinations : settings.Commands;
            var origin = wheel.CursorPosition();
            context.DispatchFeature(() => wheel.Show(id, commands, origin));
        }
        else
        {
            var command = travel ? "/hideout" : settings.Command0;
            if (!string.IsNullOrWhiteSpace(command))
                report?.Invoke(context.Execute(intent, new GameAction.SendChatCommand(command)));
        }
    }
}
