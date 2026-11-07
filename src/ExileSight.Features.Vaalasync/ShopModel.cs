using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Vaalasync;

public sealed class ShopModel(HistoryStore history, Func<string> league) : IOmniHandler
{
    public const int PricedItemPriority = int.MaxValue;
    public const string RearmMessage = "Note set — open the note box and press omni again to reprice";
    private readonly object sync = new();
    private IFeatureContext? context;
    private Guid listing;
    private ShopNote? note;
    private string custom = "";
    private bool armed, sending;
    private string status = "Open the game's note box yourself, then click one price.";
    public Action<string> ReportFailure { get; set; } = type => System.Diagnostics.Trace.TraceWarning("Shop executor failed: {0}", type);
    public HistoryStore History => history;
    public string League => league();
    public string Status { get { lock (sync) return history.RecoveryNotice is { } notice ? notice + "\n" + status : status; } }
    public bool CanApply { get { lock (sync) return armed && !sending; } }
    public ShopNote? Note { get { lock (sync) return note; } }
    public string Custom { get { lock (sync) return custom; } set { lock (sync) custom = value; Changed?.Invoke(); } }
    public event Action? Opened;
    public event Action? Changed;
    public int Priority => PricedItemPriority;
    // WPF supplies a count computed with the user's system double-click settings.
    public static bool AcceptClick(int clickCount) => clickCount == 1;
    public bool Matches(ParsedItem item, OmniContext context) => context.Game == Game.PoE1 && ShopNote.Parse(item.Note) is not null;
    internal void Register(IFeatureContext value)
    {
        context = value;
        foreach (var choice in new[] { "minus1", "plus1", "minus10", "plus10", "custom" })
            value.RegisterCommand(choice, ActionKind.SetPriceNote, token => Apply(choice, token), CommandKind.OverlayOnly);
    }
    public void Handle(ParsedItem item, OmniContext context, OmniPressKind press)
    {
        lock (sync)
        {
            if (sending) return; // Only a fresh read after the send completes may re-arm.
            var selected = ShopNote.Parse(item.Note);
            note = null;
            armed = false;
            listing = Guid.Empty;
            if (selected is null) return;
            try
            {
                listing = history.Observe(item.Name, item.BaseType ?? "", item.ItemLevel, league(), selected, DateTimeOffset.UtcNow);
                note = selected;
                armed = true;
                status = "Open the game's note box yourself before clicking a price.";
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { status = "History unavailable; repricing disabled until the listing can be saved."; }
        }
        Opened?.Invoke();
    }
    public ActionResult Apply(string choice, IntentToken? token)
    {
        ShopNote? next;
        Guid selected;
        lock (sync)
        {
            if (token is null) return new(ActionOutcome.MissingToken);
            if (!armed || sending) return new(ActionOutcome.Failed, "Read the item with omni before repricing.");
            next = note?.Suggest(choice, custom);
            if (next is null)
            {
                status = "Enter a positive price up to 1,000,000.";
                return new(ActionOutcome.Failed, status);
            }
            selected = listing;
            sending = true;
            armed = false;
            status = "Setting note…";
        }
        Changed?.Invoke();
        ActionResult result;
        try { result = context!.Execute(token, new GameAction.SetPriceNote(next.ToString())); }
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
        if (!rejectedBeforeInput)
        {
            var success = result.Outcome == ActionOutcome.Success;
            message = success ? RearmMessage : "Note possibly set — press omni again before repricing. " + message;
            if (paste is not null) message += " — " + paste;
            // PasteStatus already includes 'Sent' when supplied by ActionTransaction.
            var kind = success ? paste ?? "Sent" : "Possibly set" + (paste is null ? "" : " — " + paste);
            try { history.Change(selected, next, DateTimeOffset.UtcNow, kind); }
            catch (IOException) { message += " — history could not be saved."; }
            catch (UnauthorizedAccessException) { message += " — history access was denied."; }
        }
        lock (sync)
        {
            sending = false;
            armed = rejectedBeforeInput;
            if (!armed) note = next;
            status = message;
        }
        Changed?.Invoke();
        return result;
    }
}
