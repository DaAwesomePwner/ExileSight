using ExileSight.Core.Items;
using ExileSight.Core.Pricing;
using ExileSight.Features.Vaalasync;

namespace ExileSight.App.Features.Vaalasync;

internal static class ListingPrices
{
    internal static async Task<ShopNote?> FindAsync(PriceService prices, string? league, ParsedItem item)
    {
        if (string.IsNullOrWhiteSpace(league)) league = "Standard";
        var currency = (await prices.GetAsync(league, "Currency", TimeSpan.FromHours(1))).Overview;
        var category = (currency ?? new PriceOverview([], null)).PriceTypeFor(item);
        string[] categories = item.Rarity == ItemRarity.Unique
            ? ["UniqueWeapon", "UniqueArmour", "UniqueAccessory", "UniqueFlask", "UniqueJewel", "UniqueMap"]
            : category is null ? [] : [category];
        var results = await Task.WhenAll(categories.Select(c => prices.GetAsync(league, c, TimeSpan.FromHours(1))));
        var overview = new PriceOverview(results.Where(r => r.Overview is not null)
            .SelectMany(r => r.Overview!.Items).ToArray(), currency?.ChaosPerDivine);
        var price = overview.Find(item);
        if (price?.ChaosValue is not (> 0 and <= ShopNote.Maximum)) return null;
        return new("~b/o", price.ChaosValue, "chaos");
    }
}
