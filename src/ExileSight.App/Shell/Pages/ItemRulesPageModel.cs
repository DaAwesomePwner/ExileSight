using ExileSight.Features.ItemInfo;
using ExileSight.Features.Omni;
namespace ExileSight.App.Shell.Pages;

public sealed record ItemRulesPageModel(ItemInfoSettings Settings, OmniSettings Omni, ItemCard? Preview = null,
    int? EditingProfile = null, ItemRulesRow[]? Rows = null);
public sealed record ItemRulesRow(string Text, string Classes, Highlight Match, bool Implicit, bool Enchant)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public static ItemRulesRow[] From(HighlightProfile profile) => profile.Rules
        .GroupBy(r => (r.Text, r.Match, r.Implicit, r.Enchant, All: r.ItemClass is null))
        .Select(g => new ItemRulesRow(g.Key.Text, g.Key.All ? "" :
            string.Join(", ", g.Select(r => r.ItemClass).Distinct()), g.Key.Match, g.Key.Implicit, g.Key.Enchant)).ToArray();
}
