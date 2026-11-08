using ExileSight.Core.Features;
using ExileSight.Core.Items;
using ExileSight.Core.Pricing;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Stashninja;

public sealed class StashModel : IItemMenuProvider
{
    private SearchResult search = new(null, "Load prices first");
    public event Action? Opened;
    public Layout[] Layouts { get; init; } = [];
    public string? ProfileNotice { get; private set; }
    public string? LayoutError { get; init; }
    public ThresholdProfile[] Profiles { get; set; } =
        [new("All", 0, 10, "#CCCCCC", "#FFD700"), new("Valuable", 10, 100, "#99DDFF", "#FFD700")];
    public SearchResult Search => Volatile.Read(ref search);
    public void SetSearch(SearchResult value) => Volatile.Write(ref search, value);
    private Layout? lastLoaded;
    public Layout? SelectedLayout { get; private set; }
    public void RememberLoaded(Layout layout) => lastLoaded = layout;
    public void Open() => Open(null);
    private void Open(ParsedItem? item)
    {
        static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        bool Matches(string? value) => !string.IsNullOrWhiteSpace(value) && item is not null &&
            (Normalize(value) == Normalize(item.Name) || Normalize(value) == Normalize(item.BaseType ?? ""));
        SelectedLayout = (item is null ? null : Layouts.FirstOrDefault(l =>
            l.Cells.Any(c => Matches(c.ItemName) || Matches(c.NinjaId)))) ?? lastLoaded ??
            Layouts.FirstOrDefault(l => l.TabId == "currency" && l.Page == "general") ?? Layouts.FirstOrDefault();
        SetSearch(new(null, "Load prices first")); Opened?.Invoke();
    }
    public IReadOnlyList<ItemMenuEntry> BuildMenu(ParsedItem item) => [new("Price stash tab…", () => Open(item))];
    public void Register(IFeatureContext context)
    {
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
