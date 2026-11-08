using System.Text.Json;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Input;

// Behavior reference: Exile-UI modules/omni-key.ahk and modules/macros.ahk.
// Only the single-action clipboard sequences are retained.
public static class ActionData
{
    public static IReadOnlyList<KeyStroke> Sequence(GameAction action) => action switch
    {
        GameAction.CopyItem { Advanced: true } => [new(17,true),new(18,true),new(67,true),new(67,false),new(18,false),new(17,false)],
        GameAction.CopyItem => [new(17,true),new(67,true),new(67,false),new(17,false)],
        // FAIR-PLAY.md: Enter, Ctrl+V, Enter (one chat message). Known limitation (chat actions never use screen inspection): if the user
        // already has an unsent draft open in the chat box, the first Enter sends that draft.
        GameAction.SendChatCommand => [new(13,true),new(13,false),new(17,true),new(86,true),new(86,false),new(17,false),new(13,true),new(13,false)],
        GameAction.PasteSearch => [new(17,true),new(70,true),new(70,false),new(65,true),new(65,false),new(86,true),new(86,false),new(17,false),new(13,true),new(13,false)],
        GameAction.SetListingAmount => [new(17,true),new(65,true),new(65,false),new(86,true),new(86,false),new(17,false)],
        GameAction.SetPriceNote => [new(17,true),new(65,true),new(65,false),new(86,true),new(86,false),new(17,false),new(13,true),new(13,false)],
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };
    public static IReadOnlyList<KeyStroke> ReleaseSequence(IReadOnlyList<KeyStroke> sequence) =>
        sequence.Where(k => k.Down).Select(k => k.Key).Distinct().Reverse()
            .Select(key => new KeyStroke(key, false)).ToArray();
    public static string Audit(DateTimeOffset timestamp, string? hotkey, GameAction action, ActionOutcome outcome, string reason = "")
    {
        var verb = action is GameAction.SendChatCommand chat ? CommandVerb(chat.Text) : null;
        return JsonSerializer.Serialize(new { timestamp, hotkey, action = action.Kind.ToString(), outcome = outcome.ToString(), reason, verb });
    }
    private static string? CommandVerb(string text)
    {
        if (!text.StartsWith('/')) return null;
        var verb = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        return verb.Length <= 32 && verb.Skip(1).All(char.IsAsciiLetter) ? verb : null;
    }
}
