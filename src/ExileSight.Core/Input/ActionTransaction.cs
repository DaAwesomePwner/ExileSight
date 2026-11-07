using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Input;

/// <summary>One token-authorized transaction. Timing and native effects are injected for portable tests.</summary>
internal sealed class ActionTransaction(IClipboard clipboard, IKeySender sender, IClock clock)
{
    internal const int PasteTimeoutMilliseconds = 1500;
    private int actionsSent;
    public int ActionsSent => Volatile.Read(ref actionsSent);
    public ActionResult Execute(GameAction action, IntentRegistry.Press press)
    {
        var payload = action switch
        {
            GameAction.SendChatCommand command => command.Text,
            GameAction.PasteSearch search => search.Text,
            GameAction.SetPriceNote note => note.Text,
            _ => null
        };
        if (action is not GameAction.CopyItem &&
            (string.IsNullOrWhiteSpace(payload) || payload.Any(c => char.IsControl(c) || c is '\u2028' or '\u2029')))
            return new(ActionOutcome.Failed, "Paste text must be a single nonempty line");
        // Release-aware: never synthesize a release for modifiers the user is physically holding.
        while (sender.Modifiers != KeyModifiers.None && clock.Milliseconds - press.Timestamp < 1400)
            clock.Delay(5);
        if (sender.Modifiers != KeyModifiers.None)
            return new(ActionOutcome.Failed, "Release modifiers and press again");
        var saved = clipboard.Save(); // if saving fails, do not mutate the clipboard or send
        ActionResult result;
        try
        {
            if (payload is null) clipboard.SetText(null);
            else clipboard.OfferText(payload);
            result = SendAndWait(action, press);
        }
        catch (Exception) { result = new(ActionOutcome.Failed, "Input or clipboard operation failed"); }
        result = result with { FormatsPreserved = saved.Formats.Count, FormatsSkipped = saved.Skipped.Count };
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
        if (action is not GameAction.CopyItem)
        {
            var consumed = false;
            if (sent.Inserted > 0)
            {
                var pasteDeadline = clock.Milliseconds + PasteTimeoutMilliseconds;
                do
                {
                    clipboard.Pump();
                    consumed = clipboard.PasteConsumed;
                    if (consumed || clock.Milliseconds >= pasteDeadline) break;
                    clock.Delay(5);
                } while (true);
            }
            var status = consumed ? "Consumed" : sent.Inserted == 0 ? "Not sent" : "Timed out";
            return new(sent.Success && consumed ? ActionOutcome.Success : ActionOutcome.Failed,
                sent.Success ? consumed ? "" : "Paste timed out" : sent.Reason, PasteStatus: status);
        }
        if (!sent.Success) return new(ActionOutcome.Failed, sent.Reason);
        var deadline = clock.Milliseconds + 500;
        while (clipboard.SequenceNumber == sequence && clock.Milliseconds < deadline) clock.Delay(5);
        if (clipboard.SequenceNumber == sequence) return new(ActionOutcome.Failed, "Copy timed out");
        var text = clipboard.GetText();
        return string.IsNullOrEmpty(text)
            ? new(ActionOutcome.Failed, "No item text") : new(ActionOutcome.Success, CopiedText: text);
    }
    private string? Restore(ClipboardSnapshot saved)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try { clipboard.Restore(saved); return null; }
            catch (Exception) { if (attempt < 2) clock.Delay(25); }
        }
        // A restore can partially succeed. Never clear formats that were successfully recovered.
        return "Clipboard restore failed; preserved formats were not cleared";
    }
}
