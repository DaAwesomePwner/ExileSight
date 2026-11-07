using ExileSight.Core.Items;

namespace ExileSight.Core.Features;

/// <summary>Local app actions only; contributions receive no intent token or game-action authority.</summary>
public sealed record ItemMenuEntry(string Label, Action Execute, string? FeatureId = null);
public interface IItemMenuProvider
{
    IReadOnlyList<ItemMenuEntry> BuildMenu(ParsedItem item);
}
public static class ItemMenuEntries
{
    public static IReadOnlyList<ItemMenuEntry> Build(ParsedItem item, IEnumerable<IItemMenuProvider> providers,
        Action<Exception>? report = null)
    {
        var entries = new List<ItemMenuEntry>();
        foreach (var provider in providers)
        {
            try { entries.AddRange(provider.BuildMenu(item)); }
            catch (Exception error) { report?.Invoke(error); }
        }
        return entries.ToArray();
    }
}

// Ownership is stamped by FeatureHost, never trusted from a provider.
internal sealed class OwnedItemMenuProvider(string owner, IItemMenuProvider provider) : IItemMenuProvider
{
    public IReadOnlyList<ItemMenuEntry> BuildMenu(ParsedItem item) =>
        provider.BuildMenu(item).Select(entry => entry with { FeatureId = owner }).ToArray();
}
