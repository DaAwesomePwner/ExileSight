using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ExileSight.Platform.Windows.Input;

public sealed class ActionGate(GatePolicy policy, IClipboard clipboard, IClock clock) : IActionGate
{
    private readonly object sync = new();
    private int actionsSent;
    public int ActionsSent => Volatile.Read(ref actionsSent);
    public string AuditPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ExileSight", "logs", "actions.log");
    public ActionResult TryExecute(IntentToken? token, GameAction action)
    {
        lock (sync)
        {
            var verdict = policy.Validate(token, action, GameForeground.IsGame(), out var press);
            ActionResult result = new(verdict, verdict.ToString());
            try
            {
                // Refuse before sending if the audit destination is unavailable.
                Directory.CreateDirectory(Path.GetDirectoryName(AuditPath)!);
                using var audit = new StreamWriter(new FileStream(AuditPath, FileMode.Append, FileAccess.Write, FileShare.Read));
                if (verdict == ActionOutcome.Success)
                {
                    try { result = Execute(action, press!); }
                    catch (Exception) { result = new(ActionOutcome.Failed, "Native operation or clipboard restore failed"); }
                }
                audit.WriteLine(ActionData.Audit(clock.UtcNow, press?.HotkeyId, action, result.Outcome));
                audit.Flush();
            }
            catch (IOException) { result = new(ActionOutcome.Failed, "Audit log unavailable"); }
            catch (UnauthorizedAccessException) { result = new(ActionOutcome.Failed, "Audit log unavailable"); }
            return result;
        }
    }
    private ActionResult Execute(GameAction action, IntentRegistry.Press press)
    {
        if (action is GameAction.SendChatCommand chat &&
            (string.IsNullOrWhiteSpace(chat.Text) || chat.Text.Any(c => char.IsControl(c))))
            return new(ActionOutcome.Failed, "Chat must be a single nonempty line");
        // Never synthesize release/restore for physically held keys. Wait for real release, within token lifetime.
        while (HookHost.Modifiers() != KeyModifiers.None && clock.Milliseconds - press.Timestamp < 1400)
            Thread.Sleep(5);
        if (HookHost.Modifiers() != KeyModifiers.None) return new(ActionOutcome.Failed, "Release modifiers and press again");
        var saved = clipboard.Save();
        try
        {
            clipboard.SetText(action is GameAction.SendChatCommand command ? command.Text : null);
            var sequence = clipboard.SequenceNumber;
            if (clock.Milliseconds - press.Timestamp >= IntentRegistry.LifetimeMilliseconds)
                return new(ActionOutcome.Expired);
            if (!GameForeground.IsGame()) return new(ActionOutcome.NotForeground);
            if (HookHost.Modifiers() != KeyModifiers.None) return new(ActionOutcome.Failed, "Modifier changed");
            Send(ActionData.Sequence(action));
            Interlocked.Increment(ref actionsSent);
            if (action is GameAction.CopyItem)
            {
                var deadline = clock.Milliseconds + 500;
                while (clipboard.SequenceNumber == sequence && clock.Milliseconds < deadline) Thread.Sleep(5);
                if (clipboard.SequenceNumber == sequence) return new(ActionOutcome.Failed, "Copy timed out");
                var text = clipboard.GetText();
                return string.IsNullOrEmpty(text) ? new(ActionOutcome.Failed, "No item text") : new(ActionOutcome.Success, CopiedText: text);
            }
            Thread.Sleep(150); // allow the game to consume the paste before restoring
            return new(ActionOutcome.Success);
        }
        finally { clipboard.Restore(saved); }
    }
    private static unsafe void Send(IReadOnlyList<KeyStroke> sequence)
    {
        var inputs = new INPUT[sequence.Count];
        for (var i = 0; i < inputs.Length; i++)
        {
            inputs[i].type = INPUT_TYPE.INPUT_KEYBOARD;
            inputs[i].Anonymous.ki.wVk = (VIRTUAL_KEY)sequence[i].Key;
            inputs[i].Anonymous.ki.dwFlags = sequence[i].Down ? 0 : KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        }
        fixed (INPUT* pointer = inputs)
        {
#pragma warning disable RS0030
            var sent = PInvoke.SendInput((uint)inputs.Length, pointer, sizeof(INPUT));
#pragma warning restore RS0030
            if (sent != inputs.Length) throw new InvalidOperationException("Input rejected or incomplete");
        }
    }
}
