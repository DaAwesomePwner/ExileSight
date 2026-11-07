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
            value.RegisterCommand(choice, ActionKind.SetPriceNote, token => Apply(choice, token));
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
        catch (Exception)
        {
            lock (sync) { sending = false; status = "Send interrupted — press omni again before repricing."; }
            Changed?.Invoke();
            throw;
        }
        var message = $"{result.Outcome}: {result.Reason} {result.PasteStatus}";
        if (result.Outcome == ActionOutcome.Success)
        {
            message = RearmMessage;
            var kind = string.IsNullOrWhiteSpace(result.PasteStatus) || result.PasteStatus == "Not applicable"
                ? "Sent" : "Sent (" + result.PasteStatus + ")";
            try { history.Change(selected, next, DateTimeOffset.UtcNow, kind); }
            catch (IOException) { message += " — input sent, but history could not be saved."; }
            catch (UnauthorizedAccessException) { message += " — input sent, but history access was denied."; }
        }
        lock (sync)
        {
            sending = false;
            armed = result.Outcome != ActionOutcome.Success;
            if (!armed) note = next;
            status = message;
        }
        Changed?.Invoke();
        return result;
    }
}
