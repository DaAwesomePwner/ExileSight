using ExileSight.Core.Items;
namespace ExileSight.Core.Features;

/// <summary>Immutable, token-free observation of an omni copy, delivered before routing.</summary>
public sealed record CopiedItem(string ItemClass, string Name, string? BaseType, int? MapTier,
    IReadOnlyList<string> Mods)
{
    internal static CopiedItem From(ParsedItem item) => new(item.ItemClass, item.Name, item.BaseType,
        item.MapTier, Array.AsReadOnly(item.Mods.Select(m => m.Text).ToArray()));
}
public sealed partial class FeatureHost
{
    private readonly List<(string Owner, Action<CopiedItem> Handler)> copiedObservers = [];
}
