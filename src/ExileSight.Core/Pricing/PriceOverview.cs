using ExileSight.Core.Games;
using ExileSight.Core.Items;

namespace ExileSight.Core.Pricing;

/// <param name="History">Up to seven daily percentage changes, oldest first. Missing points remain null.</param>
public sealed record ItemPrice(string Id, string Name, string Category, string? BaseType,
    decimal ChaosValue, decimal? DivineValue, IReadOnlyList<decimal?> History, int? ListingCount, string? Confidence, int? Links = null, string? Variant = null, string? SourceCategory = null);

public sealed record PriceOverview(IReadOnlyList<ItemPrice> Items, decimal? ChaosPerDivine)
{
    public decimal? ToDivine(decimal chaos) => ChaosPerDivine is > 0 ? chaos / ChaosPerDivine : null;
    public decimal? ToChaos(decimal divine) => ChaosPerDivine is > 0 ? divine * ChaosPerDivine : null;
    public ItemPrice? Find(string idOrName, string? category = null)
        => Unique(Items.Where(p => (category is null || (PriceTypes.FromCategory(p.SourceCategory) ?? p.Category) == category) &&
            (p.Id.Equals(idOrName, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(idOrName, StringComparison.OrdinalIgnoreCase))));

    public ItemPrice? Find(ParsedItem item)
    {
        if (item.Game != Game.PoE1 || item.Unidentified) return null;
        var category = PriceTypeFor(item);
        if (item.Rarity == ItemRarity.Unique)
            return Unique(Items.Where(p => p.Category.StartsWith("Unique", StringComparison.Ordinal) &&
                p.Name == item.Name && p.BaseType == item.BaseType && p.Variant is null &&
                (item.MaximumLinks >= 5 ? p.Links == item.MaximumLinks : p.Links is null or 0)));
        return category is null ? null : Find(item.Name, category);
    }
    /// <summary>Resolve exchange metadata first, then copied base-name conventions for unloaded categories.</summary>
    public string? PriceTypeFor(ParsedItem item)
    {
        if (item.Game != Game.PoE1 || item.Rarity is ItemRarity.Rare or ItemRarity.Magic) return null;
        if (item.Rarity != ItemRarity.Unique && item.ItemClass is not
            ("Stackable Currency" or "Currency" or "Map Fragments" or "Scarab" or "Scarabs" or
             "Delve Stackable Socketable Currency" or "Divination Cards")) return null;
        var name = item.BaseType ?? item.Name;
        var matches = Items.Where(p => p.Name == item.Name || p.Name == name).ToArray();
        var types = matches.Select(p => PriceTypes.FromCategory(p.SourceCategory) ?? p.Category).Distinct().ToArray();
        if (types.Length == 1) return types[0];
        if (item.ItemClass is "Stackable Currency" or "Currency" or "Map Fragments")
        {
            var named = PriceTypes.FromName(name);
            if (named is not null) return named;
        }
        return item.ItemClass switch
        {
            "Stackable Currency" or "Currency" => "Currency",
            "Map Fragments" => "Fragment",
            "Scarab" or "Scarabs" => "Scarab",
            "Delve Stackable Socketable Currency" => "Resonator",
            "Divination Cards" => "DivinationCard",
            _ => null
        };
    }
    private static ItemPrice? Unique(IEnumerable<ItemPrice> matches)
    {
        var found = matches.Take(2).ToArray();
        return found.Length == 1 ? found[0] : null;
    }
}
