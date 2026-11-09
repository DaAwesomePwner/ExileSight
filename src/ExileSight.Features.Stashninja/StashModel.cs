using ExileSight.Core.Features;
using ExileSight.Core.Items;
using ExileSight.Core.Pricing;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Stashninja;

public sealed class StashModel : IItemMenuProvider
{
    private IFeatureContext? context;
    public bool AutomaticPrices { get; private set; } = true;
    public void SetAutomaticPrices(bool enabled)
    {
        context?.WriteSetting("automaticPrices", System.Text.Json.JsonSerializer.SerializeToElement(enabled));
        AutomaticPrices = enabled;
    }
    private SearchResult search = new(null, "Load prices first");
    public event Action? Opened;
    public Layout[] Layouts { get; init; } = [];
    public string? ProfileNotice { get; private set; }
    public string? LayoutError { get; init; }
    public ThresholdProfile[] Profiles { get; set; } =
        // TooltipHigh (low) and TooltipBest (high), from App/Ui/Tokens.xaml.
        [new("All", 0, 10, "#6AA8FF", "#6FE3D5"), new("Valuable", 10, 100, "#6AA8FF", "#6FE3D5")];
    public SearchResult Search => Volatile.Read(ref search);
    public void SetSearch(SearchResult value) => Volatile.Write(ref search, value);
    private Layout? lastLoaded;
    private Layout? selectedLayout;
    // Hotkey/item-menu dispatch can run off the UI thread; layout snapshots are immutable.
    public Layout? SelectedLayout => Volatile.Read(ref selectedLayout);
    public void RememberLoaded(Layout layout) => Volatile.Write(ref lastLoaded, layout);
    public void Open() => Open(null);
    private void Open(ParsedItem? item)
    {
        static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        bool Matches(string? value) => !string.IsNullOrWhiteSpace(value) && item is not null &&
            (Normalize(value) == Normalize(item.Name) || Normalize(value) == Normalize(item.BaseType ?? ""));
        var previous = Volatile.Read(ref lastLoaded);
        var ordered = Layouts.OrderBy(l => l.TabId, StringComparer.Ordinal)
            .ThenBy(l => l.Page, StringComparer.Ordinal).ToArray();
        var matches = item is null ? [] : ordered.Where(l =>
            l.Cells.Any(c => Matches(c.ItemName) || Matches(c.NinjaId))).ToArray();
        var selected = (previous is not null && matches.Contains(previous) ? previous : matches.FirstOrDefault()) ?? previous ??
            ordered.FirstOrDefault(l => l.TabId == "currency" && l.Page == "general") ?? ordered.FirstOrDefault();
        Volatile.Write(ref selectedLayout, selected);
        // The view invalidates search only when it actually changes/reloads the page.
        Opened?.Invoke();
    }
    public IReadOnlyList<ItemMenuEntry> BuildMenu(ParsedItem item) => [new("Price stash tab…", () => Open(item))];
    public void Register(IFeatureContext context)
    {
        this.context = context;
        AutomaticPrices = context.ReadSetting("automaticPrices")?.ValueKind != System.Text.Json.JsonValueKind.False;
        var saved = context.ReadSetting("profiles");
        if (saved is not null)
        {
            try
            {
                var profiles = System.Text.Json.JsonSerializer.Deserialize<ThresholdProfile[]>(saved.Value,
                    new System.Text.Json.JsonSerializerOptions { RespectRequiredConstructorParameters = true });
                if (profiles is not { Length: > 0 and <= 20 } || profiles.Any(p => p is null))
                    throw new ArgumentException("Expected 1–20 non-null profiles");
                foreach (var p in profiles) p.Validate();
                Profiles = profiles;
            }
            catch (Exception e) when (e is System.Text.Json.JsonException or ArgumentException)
            { ProfileNotice = "Invalid stash threshold profiles — using defaults. Check stashninja.profiles settings."; }
        }
        context.RegisterCommand("open", null, _ => { Open(); return new(ActionOutcome.Success); });
        context.RegisterDefaultBinding(new("stashninja.open", 0x4E, KeyModifiers.Control | KeyModifiers.Shift, true, "stashninja:open"));
        context.RegisterCommand("highlight", ActionKind.PasteSearch, token =>
        {
            var text = Search.Text;
            if (string.IsNullOrEmpty(text) || text.Length > 250) return new(ActionOutcome.Failed, "No safe search available");
            return context.Execute(token, new GameAction.PasteSearch(text));
        }, CommandKind.OverlayOnly);
        context.RegisterItemMenu(this);
        context.RegisterOverlay(this);
    }
}
