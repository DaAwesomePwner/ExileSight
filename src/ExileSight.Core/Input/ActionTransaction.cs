using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Input;

/// <summary>One token-authorized transaction. Timing and native effects are injected for portable tests.</summary>
internal sealed class ActionTransaction(IClipboard clipboard, IKeySender sender, IClock clock)
{
    internal const int PasteRetentionMilliseconds = 750;
    private int actionsSent;
    public int ActionsSent => Volatile.Read(ref actionsSent);
    public ActionResult Execute(GameAction action, IntentRegistry.Press press)
    {
        if (action is GameAction.SendChatCommand chat &&
            (string.IsNullOrWhiteSpace(chat.Text) || chat.Text.Any(char.IsControl)))
            return new(ActionOutcome.Failed, "Chat must be a single nonempty line");
        // Release-aware: never synthesize a release for modifiers the user is physically holding.
        while (sender.Modifiers != KeyModifiers.None && clock.Milliseconds - press.Timestamp < 1400)
            clock.Delay(5);
        if (sender.Modifiers != KeyModifiers.None)
            return new(ActionOutcome.Failed, "Release modifiers and press again");
        var saved = clipboard.Save(); // if saving fails, do not mutate the clipboard or send
        ActionResult result;
        try
        {
            clipboard.SetText(action is GameAction.SendChatCommand command ? command.Text : null);
            result = SendAndWait(action, press);
        }
        catch (Exception) { result = new(ActionOutcome.Failed, "Input or clipboard operation failed"); }
        var recovery = Restore(saved);
        return recovery is null ? result : result with
        {
            Outcome = ActionOutcome.Failed,
            Reason = string.IsNullOrEmpty(result.Reason) ? recovery : result.Reason + "; " + recovery
        };
    }
    private ActionResult SendAndWait(GameAction action, IntentRegistry.Press press)
    {
        var sequence = clipboard.SequenceNumber;
        if (clock.Milliseconds - press.Timestamp >= IntentRegistry.LifetimeMilliseconds)
            return new(ActionOutcome.Expired, "Intent expired before sending");
        if (!sender.IsGameForeground) return new(ActionOutcome.NotForeground, "Game lost foreground");
        if (sender.Modifiers != KeyModifiers.None) return new(ActionOutcome.Failed, "Modifier changed");
        var sent = sender.Send(ActionData.Sequence(action));
        if (sent.Inserted > 0) Interlocked.Increment(ref actionsSent);
        // Bounded spike compromise, NOT proof of a paste read. Also retain after partial chat insertion.
        if (action is GameAction.SendChatCommand && sent.Inserted > 0)
            clock.Delay(PasteRetentionMilliseconds);
        if (!sent.Success) return new(ActionOutcome.Failed, sent.Reason);
        if (action is GameAction.SendChatCommand) return new(ActionOutcome.Success);
        var deadline = clock.Milliseconds + 500;
        while (clipboard.SequenceNumber == sequence && clock.Milliseconds < deadline) clock.Delay(5);
        if (clipboard.SequenceNumber == sequence) return new(ActionOutcome.Failed, "Copy timed out");
        var text = clipboard.GetText();
        return string.IsNullOrEmpty(text)
            ? new(ActionOutcome.Failed, "No item text") : new(ActionOutcome.Success, CopiedText: text);
    }
    private string? Restore(string? saved)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try { clipboard.Restore(saved); return null; }
            catch (Exception) { if (attempt < 2) clock.Delay(25); }
        }
        // Never deliberately leave the chat payload behind when restoration is impossible.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try { clipboard.SetText(null); return "Clipboard restore failed; clipboard cleared"; }
            catch (Exception) { if (attempt < 2) clock.Delay(25); }
        }
        return "Clipboard restore and clear failed; clipboard may still contain action text";
    }
}
