using ExileSight.Features.ItemInfo;
using ExileSight.Features.Omni;
namespace ExileSight.App.Shell.Pages;

public sealed record ItemRulesPageModel(ItemInfoSettings Settings, OmniSettings Omni, ItemCard? Preview = null);
public sealed record ItemRulesRow(string Text, string Classes, Highlight Match, bool Implicit, bool Enchant)
{
    public static ItemRulesRow[] From(HighlightProfile profile) => profile.Rules
        .GroupBy(r => (r.Text, r.Match, r.Implicit, r.Enchant))
        .Select(g => new ItemRulesRow(g.Key.Text, g.Any(r => r.ItemClass is null) ? "" :
            string.Join(", ", g.Select(r => r.ItemClass)), g.Key.Match, g.Key.Implicit, g.Key.Enchant)).ToArray();
}
