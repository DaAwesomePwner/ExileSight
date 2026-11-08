using ExileSight.Core.Items;

namespace ExileSight.Core.Features;

public static class TooltipDismissal
{
    public static bool MovedBeyond(double x, double y, double originX, double originY, double scale) =>
        Math.Pow(x - originX, 2) + Math.Pow(y - originY, 2) > Math.Pow(30 * scale, 2);
}

public interface IItemCardComparisonProvider
{
    ItemCardComparison? BuildComparison(ParsedItem item);
}

public static class ItemCardComparisons
{
    public static ItemCardComparison? Build(ParsedItem item,
        IEnumerable<IItemCardComparisonProvider> providers, Action<Exception>? report = null)
    {
        var slots = new List<ItemCardCompareSlot>();
        var notes = new List<string>();
        foreach (var provider in providers)
        {
            try
            {
                if (provider.BuildComparison(item) is not { } comparison) continue;
                slots.AddRange(comparison.Slots);
                if (!string.IsNullOrEmpty(comparison.Note)) notes.Add(comparison.Note);
            }
            catch (Exception error) { report?.Invoke(error); }
        }
        return slots.Count == 0 ? null : new(slots, string.Join(" · ", notes));
    }
}
