using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Vaalasync;

public sealed class ShopModel(HistoryStore history, Func<string> league) : IOmniHandler
{
    private readonly object sync = new();
    private IFeatureContext? context;
    private Guid listing;
    private ShopNote? note;
    private string custom = "";
    public HistoryStore History => history;
    public string League => league();
    public string Status { get; private set; } = "Open the game's note box yourself, then click one price.";
    public ShopNote? Note { get { lock (sync) return note; } }
    public string Custom { get { lock (sync) return custom; } set { lock (sync) custom = value; Changed?.Invoke(); } }
    public event Action? Opened;
    public event Action? Changed;
    public int Priority => int.MaxValue;
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
            var selected = ShopNote.Parse(item.Note);
            note = null;
            listing = Guid.Empty;
            if (selected is null) return;
            try
            {
                listing = history.Observe(item.Name, item.BaseType ?? "", item.ItemLevel, league(), selected, DateTimeOffset.UtcNow);
                note = selected;
                Status = "Open the game's note box yourself before EACH price click.";
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { Status = "History unavailable; repricing disabled until the listing can be saved."; }
        }
        Opened?.Invoke();
    }
    public ActionResult Apply(string choice, IntentToken? token)
    {
        lock (sync)
        {
            var next = note?.Suggest(choice, custom);
            var result = token is null ? new ActionResult(ActionOutcome.MissingToken) :
                next is null ? new(ActionOutcome.Failed, "Enter a positive price up to 1,000,000.") :
                context!.Execute(token, new GameAction.SetPriceNote(next.ToString()));
            Status = $"{result.Outcome}: {result.Reason} {result.PasteStatus}";
            if (result.Outcome == ActionOutcome.Success && next is not null)
            {
                note = next;
                try { history.Change(listing, next, DateTimeOffset.UtcNow, "Sent (" + result.PasteStatus + ")"); }
                catch (IOException) { Status += " — input sent, but history could not be saved."; }
                catch (UnauthorizedAccessException) { Status += " — input sent, but history access was denied."; }
            }
            Changed?.Invoke();
            return result;
        }
    }
}
