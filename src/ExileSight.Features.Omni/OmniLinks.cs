// URL behavior derived from Exile-UI modules/omni-key.ahk lines ~389–553 (MIT).
// PoEDB deliberately uses the base type, falling back to item class for unresolved magic bases.
// These are browser destinations only, never in-process network hosts.
using System.Collections.Frozen;
using System.Text.Json;
using ExileSight.Core.Items;

namespace ExileSight.Features.Omni;

public sealed record OmniLink(string Label, string Url);
public static class OmniLinks
{
    public static FrozenSet<string> AllowedHosts { get; } = new[]
    { "www.poewiki.net", "poedb.tw", "www.craftofexile.com", "www.pathofexile.com" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyList<OmniLink> Build(ParsedItem item, string? league = null) => Build(item, league, true);
    internal static IReadOnlyList<OmniLink> Build(ParsedItem item, string? league, bool includeTrade)
    {
        var unique = item.Rarity == ItemRarity.Unique;
        var name = item.Name.StartsWith("Foulborn ", StringComparison.Ordinal) ? item.Name[9..] : item.Name;
        var page = unique ? name : item.BaseType ?? item.ItemClass;
        var query = new Dictionary<string, string>();
        if (unique) query["name"] = name;
        if (item.BaseType is { Length: > 0 } type) query["type"] = type;
        var json = JsonSerializer.Serialize(new { query });
        var links = new List<OmniLink>
        {
            new("PoE Wiki", "https://www.poewiki.net/wiki/" + Page(page)),
            new("PoEDB", "https://poedb.tw/us/" + Page(item.BaseType ?? item.ItemClass)),
            new("Craft of Exile", "https://www.craftofexile.com/?game=poe1")
        };
        if (includeTrade) links.Add(new("Trade search", "https://www.pathofexile.com/trade/search/" +
                Segment(OmniLeague.Select(league)) +
                "?q=" + Uri.EscapeDataString(json)));
        return links.AsReadOnly();
    }
    private static string Page(string value) => Segment(value.Replace(' ', '_'));
    private static string Segment(string value)
    {
        if (value is "." or "..") throw new ArgumentException("Dot path segments are not allowed", nameof(value));
        return Uri.EscapeDataString(value);
    }
}
