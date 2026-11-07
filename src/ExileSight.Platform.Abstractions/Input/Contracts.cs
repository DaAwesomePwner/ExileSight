namespace ExileSight.Platform.Abstractions.Input;

public enum ActionKind { CopyItem, SendChatCommand }
public abstract record GameAction
{
    private GameAction() { }
    public sealed record CopyItem(bool Advanced = false) : GameAction;
    public sealed record SendChatCommand(string Text) : GameAction;
    public ActionKind Kind => this is CopyItem ? ActionKind.CopyItem : ActionKind.SendChatCommand;
}

/// <summary>Identity alone confers no authority: only a registered physical press is accepted.</summary>
public sealed class IntentToken;
public enum ActionOutcome { Success, MissingToken, UsedOrUnknownToken, Expired, NotForeground, Undeclared, WrongAction, Failed }
public sealed record ActionResult(ActionOutcome Outcome, string Reason = "", string? CopiedText = null, bool AuditFailed = false);
public interface IActionGate
{
    string AuditPath { get; }
    int ActionsSent { get; }
    ActionResult TryExecute(IntentToken? token, GameAction action);
}
/// <summary>Phase 0 preserves Unicode text or empty only; other formats are not retained.</summary>
public interface IClipboard
{
    string? GetText();
    void SetText(string? text);
    string? Save();
    void Restore(string? saved);
    uint SequenceNumber { get; }
}
[Flags]
public enum KeyModifiers { None = 0, Control = 1, Alt = 2, Shift = 4, Windows = 8 }
public sealed record HotkeyBinding(string Id, int Key, KeyModifiers Modifiers, bool Block, GameAction Action);
public interface IHotkeys : IDisposable
{
    bool IsAlive { get; }
    void Start();
}
