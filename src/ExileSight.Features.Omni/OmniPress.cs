using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Omni;

/// <summary>Serialized by the hook queue consumer. Copy at down keeps the original token fresh.
/// Missing releases are dropped: the next down replaces the captured item.</summary>
public sealed class OmniPress(IFeatureContext context, OmniMenu menu)
{
    public const long LongPressMilliseconds = 200;
    private long lastId = -1;
    private PressStarted? pending;
    private ParsedItem? item;
    public ActionResult Start(IntentToken? token, PressStarted press)
    {
        if (press.Id <= lastId) return new(ActionOutcome.UsedOrUnknownToken);
        lastId = press.Id;
        pending = null;
        item = null;
        var result = context.Execute(token, new GameAction.CopyItem());
        if (result.Outcome != ActionOutcome.Success || string.IsNullOrWhiteSpace(result.CopiedText)) return result;
        var parsed = ItemTextParser.Parse(result.CopiedText, Game.PoE1);
        if (parsed.Item is not null && !parsed.Warnings.Any(w => w.Severity == WarningSeverity.Error))
        { pending = press; item = parsed.Item; }
        return result;
    }
    public void Release(PressReleased release)
    {
        if (pending is not { } down || down.Id != release.Id || down.Timestamp != release.DownTimestamp) return;
        var captured = item;
        pending = null;
        item = null;
        if (captured is null || release.UpTimestamp < down.Timestamp) return;
        if (release.UpTimestamp - down.Timestamp > LongPressMilliseconds) menu.Show(captured);
        else new OmniRouter(context.OmniHandlers).Route(captured, new(Game.PoE1));
    }
}
