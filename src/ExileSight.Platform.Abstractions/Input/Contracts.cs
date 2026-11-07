namespace ExileSight.Platform.Abstractions.Input;

public enum ActionKind { CopyItem, SendChatCommand, PasteSearch, SetPriceNote }
public abstract record GameAction
{
    private GameAction() { }
    public sealed record CopyItem(bool Advanced = false) : GameAction;
    public sealed record SendChatCommand(string Text) : GameAction;
    public sealed record PasteSearch(string Text) : GameAction;
    public sealed record SetPriceNote(string Text) : GameAction;
    public ActionKind Kind => this switch
    {
        CopyItem => ActionKind.CopyItem, SendChatCommand => ActionKind.SendChatCommand,
        PasteSearch => ActionKind.PasteSearch, SetPriceNote => ActionKind.SetPriceNote,
        _ => throw new InvalidOperationException()
    };
}

/// <summary>Identity alone confers no authority: only a registered physical press is accepted.</summary>
public sealed class IntentToken;
public enum ActionOutcome { Success, MissingToken, UsedOrUnknownToken, Expired, NotForeground, Undeclared, WrongAction, Failed }
public sealed record ActionResult(ActionOutcome Outcome, string Reason = "", string? CopiedText = null, bool AuditFailed = false, int FormatsPreserved = 0, int FormatsSkipped = 0, string PasteStatus = "Not applicable");
public interface IActionGate
{
    string AuditPath { get; }
    int ActionsSent { get; }
    ActionResult TryExecute(IntentToken? token, GameAction action);
    ActionResult TryExecute(IntentToken? token, GameAction action, string featureId) =>
        new(ActionOutcome.Undeclared, "Scoped execution is not supported by this gate");
}
public sealed record ClipboardSnapshot(IReadOnlyDictionary<uint, byte[]> Formats, IReadOnlyList<uint> Skipped);

/// <summary>Snapshots own copied bytes, never borrowed native handles.</summary>
public interface IClipboard
{
    string? GetText();
    string? GetText(int timeoutMilliseconds) => GetText();
    void SetText(string? text);
    ClipboardSnapshot Save();
    void Restore(ClipboardSnapshot? saved);
    void OfferText(string text);
    bool PasteConsumed { get; }
    void Pump();
    void AbandonOffer() { }
    int RecoveredFormats => 0;
    uint SequenceNumber { get; }
}
[Flags]
public enum KeyModifiers { None = 0, Control = 1, Alt = 2, Shift = 4, Windows = 8 }
public sealed record HotkeyBinding(string Id, int Key, KeyModifiers Modifiers, bool Block, GameAction? Action,
    string Context = "gameFocused", string? FeatureCommand = null, string FeatureId = "builtin",
    ActionKind? CommandAction = null, bool TrackRelease = false);
public interface IHotkeys : IDisposable
{
    bool IsAlive { get; }
    void Start();
}

/// <summary>Feature commands receive authority only from a registered physical intent.</summary>
public interface IFeatureCommandHandler
{
    string Command { get; }
    string FeatureId { get; }
    ActionKind? RequiredAction => null;
    ActionResult Execute();
    ActionResult Execute(IntentToken? token) => Execute();
}
public interface IHotkeyContexts
{
    void Register(string context);
    void SetActive(string context, bool active);
}
