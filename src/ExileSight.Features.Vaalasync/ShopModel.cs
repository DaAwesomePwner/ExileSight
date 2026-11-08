using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Vaalasync;

public sealed partial class ShopModel(HistoryStore history, Func<string> league) : IOmniHandler, IItemMenuProvider
{
    public const int PricedItemPriority = int.MaxValue;
    public const string RearmMessage = "Price sent — finish listing manually; press omni again to reprice";
    private readonly object sync = new();
    private IFeatureContext? context;
    private Task<ReadHistory?>? currentRead;
    private Task historyTail = Task.CompletedTask;
    private string currency = "chaos";
    public bool NoteMode { get; private set; }
    public Func<ParsedItem, string, Task<ShopNote?>> SeedPrice { get; set; } = (_, _) => Task.FromResult<ShopNote?>(null);
    public Func<Task<string>> ResolveLeagueAsync { get; set; } = () => Task.FromResult(league());
    private long revision;
    private string? currentCurrency;
    public string Currency { get { lock (sync) return currency; } set { lock (sync) { currency = value; revision++; } NotifyChanged(); } }
    public string Instructions => NoteMode ? "Open the game’s note box before clicking a price." :
        "Open Set Item Price and focus its amount field. Choose the currency and click LIST ITEM yourself.";
    public IReadOnlyList<ItemMenuEntry> BuildMenu(ParsedItem value) => value.Game != Game.PoE1 || ShopNote.Parse(value.Note) is not null ? [] :
        [new("Set listing price…", () => Handle(value, new(Game.PoE1), OmniPressKind.Long))];
    private ShopNote? note, estimate;
    private string custom = "";
    private bool armed, sending;
    private string status = "Open Set Item Price and focus its amount field. Choose the currency and click LIST ITEM yourself.";
    public Action<string> ReportFailure { get; set; } = type => System.Diagnostics.Trace.TraceWarning("Shop executor failed: {0}", type);
    public HistoryStore History => history;
    public string Status { get { lock (sync) return history.RecoveryNotice is { } notice ? notice + "\n" + status : status; } }
    public bool CanApply { get { lock (sync) return armed && !sending; } }
    public ShopNote? Note { get { lock (sync) return note; } }
    public string Custom { get { lock (sync) return custom; } set { lock (sync) { custom = value; revision++; } NotifyChanged(); } }
    public event Action? Opened;
    public event Action? Changed;
    public int Priority => PricedItemPriority;
    // WPF supplies a count computed with the user's system double-click settings.
    public static bool AcceptClick(int clickCount) => clickCount == 1;
    public bool Matches(ParsedItem item, OmniContext context) => context.Game == Game.PoE1 && ShopNote.Parse(item.Note) is not null;
    internal void Register(IFeatureContext value)
    {
        context = value;
        NoteMode = value.ReadSetting("listingMode") is { ValueKind: System.Text.Json.JsonValueKind.String } mode && mode.GetString() == "note";
        status = Instructions;
        foreach (var choice in new[] { "minus1", "plus1", "minus10", "plus10", "custom" })
            value.RegisterCommand(choice, NoteMode ? ActionKind.SetPriceNote : ActionKind.SetListingAmount, token => Apply(choice, token), CommandKind.OverlayOnly);
    }
    public void Handle(ParsedItem item, OmniContext context, OmniPressKind press)
    {
        long seedRevision;
        Task<ReadHistory?> read;
        lock (sync)
        {
            if (sending) return; // Only a fresh read after the send completes may re-arm.
            var current = ShopNote.Parse(item.Note);
            currentCurrency = current?.Currency;
            seedRevision = ++revision;
            note = current;
            estimate = null;
            armed = true;
            currency = current?.Currency ?? "chaos";
            custom = "";
            status = Instructions;
            read = RecordReadAsync(item, ResolveLeagueAsync(), historyTail, ++readRevision, DateTimeOffset.UtcNow);
            currentRead = read;
            historyTail = read;
        }
        Opened?.Invoke();
        _ = PrepareAsync(item, seedRevision, read);
    }
    public ActionResult Apply(string choice, IntentToken? token)
    {
        ShopNote? next;
        Task<ReadHistory?> selected;
        lock (sync)
        {
            if (token is null) return new(ActionOutcome.MissingToken);
            if (!armed || sending) return new(ActionOutcome.Failed, "Read the item with omni before repricing.");
            next = Suggest(choice);
            if (next is null)
            {
                status = !NoteMode && choice == "custom" && ShopNote.ParseAmount(custom) is { } amount && amount != decimal.Truncate(amount)
                    ? "Async listing amounts must be a whole number." : "Enter a positive price up to 1,000,000.";
                return new(ActionOutcome.Failed, status);
            }
            selected = currentRead!;
            revision++;
            sending = true;
            armed = false;
            status = NoteMode ? "Setting note…" : "Setting amount…";
        }
        NotifyChanged();
        ActionResult result;
        try { result = context!.Execute(token, NoteMode ? new GameAction.SetPriceNote(next.ToString()) : new GameAction.SetListingAmount(next.Amount)); }
        catch (Exception error)
        {
            try { ReportFailure(error.GetType().Name); }
            catch (Exception) { /* Preserve the uncertain outcome even if logging fails. */ }
            // An exception cannot establish whether native input was already inserted.
            result = new(ActionOutcome.Failed, "Send interrupted; input may already have been sent.");
        }
        var rejectedBeforeInput = result.Outcome is ActionOutcome.MissingToken or ActionOutcome.UsedOrUnknownToken
            or ActionOutcome.Expired or ActionOutcome.NotForeground or ActionOutcome.Undeclared or ActionOutcome.WrongAction;
        var paste = string.IsNullOrWhiteSpace(result.PasteStatus) || result.PasteStatus == "Not applicable"
            ? null : result.PasteStatus;
        var message = $"{result.Outcome}: {result.Reason}";
        string? historyKind = null;
        if (!rejectedBeforeInput)
        {
            var success = result.Outcome == ActionOutcome.Success;
            message = success ? RearmMessage : "Price possibly set — press omni again before repricing. " + message;
            if (paste is not null) message += " — " + paste;
            // PasteStatus already includes 'Sent' when supplied by ActionTransaction.
            historyKind = success ? paste ?? "Sent" : "Possibly set" + (paste is null ? "" : " — " + paste);
        }
        lock (sync)
        {
            sending = false;
            armed = rejectedBeforeInput;
            status = message;
            if (historyKind is not null)
                historyTail = RecordSendAsync(selected, historyTail, next, historyKind, DateTimeOffset.UtcNow);
        }
        NotifyChanged();
        return result;
    }
}
