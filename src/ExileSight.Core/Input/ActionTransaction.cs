using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Input;

/// <summary>One token-authorized transaction. Timing and native effects are injected for portable tests.</summary>
internal sealed class ActionTransaction(IClipboard clipboard, IKeySender sender, IClock clock)
{
    internal const int NativeHoldMilliseconds = 750, StreamingHoldMilliseconds = 1500;
    private int actionsSent;
    public int ActionsSent => Volatile.Read(ref actionsSent);
    public ActionResult Execute(GameAction action, IntentRegistry.Press press)
    {
        var payload = action switch
        {
            GameAction.SendChatCommand command => command.Text,
            GameAction.PasteSearch search => search.Text,
            GameAction.SetPriceNote note => note.Text,
            GameAction.SetListingAmount amount => amount.Text,
            _ => null
        };
        if (action is not GameAction.CopyItem &&
            (string.IsNullOrWhiteSpace(payload) || payload.Any(c => char.IsControl(c) || c is '\u2028' or '\u2029')))
            return new(ActionOutcome.Failed, "Paste text must be a single nonempty line");
        // Release-aware: never synthesize a release for modifiers the user is physically holding.
        while (sender.Modifiers != KeyModifiers.None && clock.Milliseconds - press.Timestamp < 1400)
            Wait(5);
        if (sender.Modifiers != KeyModifiers.None)
            return new(ActionOutcome.Failed, "Release modifiers and press again");
        ClipboardSnapshot saved;
        try { saved = clipboard.Save(); }
        catch (Exception) { return new(ActionOutcome.Failed, "Clipboard could not be preserved"); }
        ActionResult result;
        try
        {
            if (payload is null) clipboard.SetText(null);
            else clipboard.OfferText(payload);
            result = SendAndWait(action, press);
        }
        catch (Exception) { result = new(ActionOutcome.Failed, "Input or clipboard operation failed"); }
        if (saved.Skipped.Count > 0)
            result = result with { Reason = Append(result.Reason,
                "Clipboard formats not preserved (unsupported or over budget): " + string.Join(", ", saved.Skipped.Select(id => $"0x{id:X4}"))) };
        result = result with { FormatsPreserved = saved.Formats.Count, FormatsSkipped = saved.Skipped.Count };
        var recovery = Restore(saved);
        if (result.Outcome == ActionOutcome.Success && action is not GameAction.CopyItem && clipboard.PasteConsumed)
            result = result with { PasteStatus = "Sent (paste confirmed)",
                Reason = result.Reason.Replace("Sent (unconfirmed)", "Sent (paste confirmed)", StringComparison.Ordinal) };
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
        var hold = sender.IsStreaming ? StreamingHoldMilliseconds : NativeHoldMilliseconds;
        KeySendResult sent;
        var sendThrew = false;
        try { sent = sender.Send(ActionData.Sequence(action)); }
        catch (Exception)
        {
            // Native insertion may already have happened. Do not shorten the clipboard hold.
            sendThrew = true;
            sent = new(false, 0, "Input send threw; insertion count unknown");
        }
        if (sent.Inserted > 0) Interlocked.Increment(ref actionsSent);
        if (action is not GameAction.CopyItem)
        {
            var consumed = false;
            Exception? renderError = null;
            if (sent.Inserted > 0 || sendThrew)
            {
                var pasteDeadline = clock.Milliseconds + hold;
                do
                {
                    try { clipboard.Pump(); consumed = clipboard.PasteConsumed; }
                    catch (Exception error) { renderError = error; }
                    if (clock.Milliseconds >= pasteDeadline) break;
                    Wait(5);
                } while (true);
            }
            if (renderError is not null) return new(ActionOutcome.Failed, "Clipboard rendering failed after send");
            var status = sent.Success ? consumed ? "Sent (paste confirmed)" : "Sent (unconfirmed)" : "Not sent completely";
            return new(sent.Success ? ActionOutcome.Success : ActionOutcome.Failed,
                sent.Success ? status : sent.Reason, PasteStatus: status);
        }
        if (sendThrew) Wait(hold); // A partial copy can still arrive after the sender throws.
        if (!sent.Success) return new(ActionOutcome.Failed, sent.Reason);
        var deadline = clock.Milliseconds + (hold == StreamingHoldMilliseconds ? StreamingHoldMilliseconds : 500);
        while (clipboard.SequenceNumber == sequence && clock.Milliseconds < deadline) Wait(5);
        if (clipboard.SequenceNumber == sequence) return new(ActionOutcome.Failed, "Copy timed out");
        var remaining = deadline - clock.Milliseconds;
        if (remaining <= 0) return new(ActionOutcome.Failed, "Copy timed out");
        string? text;
        try { text = clipboard.GetText((int)remaining); }
        catch (TimeoutException) { return new(ActionOutcome.Failed, "Copy timed out reading clipboard text"); }
        return string.IsNullOrEmpty(text)
            ? new(ActionOutcome.Failed, "No item text") : new(ActionOutcome.Success, CopiedText: text);
    }
    private string? Restore(ClipboardSnapshot saved)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try { PumpForRecovery(); clipboard.Restore(saved); return null; }
            catch (Exception) { if (attempt < 2) Wait(25); }
        }
        if (clipboard.RecoveredFormats > 0)
        {
            clipboard.AbandonOffer();
            return $"Clipboard restore incomplete; {clipboard.RecoveredFormats} user formats recovered; action text removed";
        }
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try { PumpForRecovery(); clipboard.SetText(null); return "Clipboard restore failed; action text cleared"; }
            catch (Exception) { if (attempt < 2) Wait(25); }
        }
        clipboard.AbandonOffer();
        return "Clipboard restore and clear failed; action text may remain; offer abandoned";
    }
    private static string Append(string reason, string detail) => string.IsNullOrEmpty(reason) ? detail : reason + "; " + detail;
    private void PumpForRecovery()
    {
        try { clipboard.Pump(); }
        catch (Exception) { /* A render error must not prevent restoring the saved clipboard. */ }
    }
    private void Wait(int milliseconds)
    {
        var deadline = clock.Milliseconds + milliseconds;
        do
        {
            PumpForRecovery();
            var remaining = deadline - clock.Milliseconds;
            if (remaining <= 0) break;
            clock.Delay((int)Math.Min(5, remaining));
        } while (true);
    }
}
