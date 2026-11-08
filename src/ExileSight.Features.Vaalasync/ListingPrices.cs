using ExileSight.Core.Items;
using ExileSight.Core.Pricing;

namespace ExileSight.Features.Vaalasync;

public static class ListingPrices
{
    public static Task<ShopNote?> FindAsync(PriceService prices, string league, ParsedItem item) =>
        FindAsync((name, category) => prices.GetAsync(name, category, TimeSpan.FromHours(1)), league, item);

    public static async Task<ShopNote?> FindAsync(Func<string, string, Task<PriceResult>> fetch, string league, ParsedItem item)
    {
        var currency = (await fetch(league, "Currency")).Overview;
        var category = (currency ?? new PriceOverview([], null)).PriceTypeFor(item);
        string[] categories = item.Rarity == ItemRarity.Unique
            ? ["UniqueWeapon", "UniqueArmour", "UniqueAccessory", "UniqueFlask", "UniqueJewel", "UniqueMap"]
            : category is null ? [] : [category];
        var results = await Task.WhenAll(categories.Select(c => fetch(league, c)));
        var overview = new PriceOverview(results.Where(r => r.Overview is not null)
            .SelectMany(r => r.Overview!.Items).ToArray(), currency?.ChaosPerDivine);
        var price = overview.Find(item);
        if (price?.ChaosValue is not (> 0 and <= ShopNote.Maximum)) return null;
        return new("~b/o", price.ChaosValue, "chaos");
    }
}
