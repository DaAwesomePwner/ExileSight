using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.SearchStrings;

[DeclaredActions(ActionKind.PasteSearch)]
public sealed class SearchStringsFeature : IFeature
{
    public FeatureManifest Manifest { get; } = new("searchstr", [Game.PoE1], [ActionKind.PasteSearch], [], [],
        "F9 opens manual vendor selection; one physical selection click highlights one saved search.");
    public SearchMenu Menu { get; } = new();
    public void Register(IFeatureContext context)
    {
        void Load()
        {
            try { Menu.Configure(SearchSettings.Load(context.ReadSetting("vendors"))); Menu.Notice = null; }
            catch (Exception error) when (error is JsonException or ArgumentException)
            {
                Menu.Configure([]);
                Menu.Notice = "Invalid searchstr.vendors settings; correct the lists and reopen with F9.";
            }
        }
        Load();
        context.RegisterCommand("open", null, _ => { Load(); Menu.Open(); return new(ActionOutcome.Success); });
        // F7 = copy spike, F8 = hideout; F9 yields to persisted searchstr.open bindings.
        context.RegisterDefaultBinding(new("searchstr.open", 0x78, KeyModifiers.None, true, "searchstr:open"));
        context.RegisterCommand("paste", ActionKind.PasteSearch, token => Menu.Paste(token, context.Execute));
        context.RegisterOverlay(Menu);
    }
}
