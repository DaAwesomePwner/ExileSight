// Behaviour source: Lailloken/Exile-UI modules/map-info.ahk (MIT), D-082 pin.
using System.Globalization;
using System.Text.RegularExpressions;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Items;

namespace ExileSight.Features.MapInfo;

public sealed record MapRow(string? Id, string Text, string? Area, int Rank = 0);
public sealed record MapResult(string Name, decimal? Quantity, decimal? Rarity, decimal? PackSize, IReadOnlyList<MapRow> Mods);

public sealed class MapMatcher
{
    private readonly Dictionary<string, MapModData[]> patterns;
    private readonly int maxLines;
    private static readonly Regex Ranges = new(@"\([+-]?\d+(?:\.\d+)?-[+-]?\d+(?:\.\d+)?\)", RegexOptions.CultureInvariant);
    private static readonly Regex Numbers = new(@"[+-]?\d+(?:\.\d+)?%?", RegexOptions.CultureInvariant);
    private static readonly Regex Markers = new(@" \((implicit|enchant|crafted|fractured)\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Dictionary<string, string> Npcs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Black Scythe Mercenaries"] = "Tujen", ["Order of the Chalice"] = "Rog",
        ["Druids of the Broken Circle"] = "Gwennen", ["Knights of the Sun"] = "Dannig"
    };
    public MapMatcher(MapModsData data)
    {
        patterns = data.Entries.SelectMany(e => e.Patterns.Select(p => (Key: Normalize(p), Entry: e)))
            .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Select(p => p.Entry).Distinct().ToArray());
        maxLines = patterns.Count == 0 ? 1 : patterns.Keys.Max(k => k.Count(c => c == '|') + 1);
    }
    private static string Normalize(string text) => Numbers.Replace(Ranges.Replace(Markers.Replace(text, ""), ""), "#")
        .Trim().ToLowerInvariant();
    public MapResult Match(ParsedItem item)
    {
        var rows = new List<MapRow>();
        var totals = new Dictionary<(string Id, string? Area), (int Index, decimal[] Values)>();
        string? area = null;
        var logbook = item.ItemClass == "Expedition Logbooks";
        if (!logbook && item.Rarity is ItemRarity.Normal or ItemRarity.Unique)
            return new(item.Name, item.MapQuantity, item.MapRarity, item.MapPackSize, rows);
        for (var i = 0; i < item.Mods.Count;)
        {
            // Labels immediately preceding a recognised faction identify the copied expedition area.
            if (logbook && i + 1 < item.Mods.Count && Npcs.TryGetValue(item.Mods[i + 1].Text, out var npc))
            { area = item.Mods[i].Text + " (" + npc + ")"; i += 2; continue; }
            var first = item.Mods[i];
            // Plain copies have no affix headers; advanced map copies must be prefix/suffix blocks.
            if (!logbook && (!first.Kind.HasFlag(ModKind.Explicit) ||
                first.Header is not null && first.AffixType == AffixType.Unknown) ||
                first.Text.Contains("also apply to rarity", StringComparison.OrdinalIgnoreCase))
            { i++; continue; }
            MapModData? match = null; var consumed = 1;
            for (var length = Math.Min(maxLines, item.Mods.Count - i); length > 0; length--)
            {
                var lines = item.Mods.Skip(i).Take(length).ToArray();
                if (lines.Any(m => m.Group != first.Group || m.Kind != first.Kind)) continue;
                var key = string.Join('|', lines.Select(m => Normalize(m.Text)));
                // Existing modded-logbook plain/advanced fixtures use this explicit spelling.
                // Restrict the alias to logbook explicits; pack text and area implicits stay exact.
                if (logbook && first.Kind.HasFlag(ModKind.Explicit) && key == "# increased number of remnants")
                    key = "area contains # increased number of remnants";
                if (!patterns.TryGetValue(key, out var candidates)) continue;
                var applicable = candidates.Where(e => e.Type != "expedition" || logbook)
                    .DistinctBy(e => e.Id).ToArray();
                if (applicable.Length != 1) continue; // Ambiguity is unknown, never a guess.
                match = applicable[0]; consumed = length; break;
            }
            var scope = first.Kind.HasFlag(ModKind.Implicit) ? area : null;
            if (match is null) rows.Add(new(null, "unknown: " + first.Text, scope, 3));
            else
            {
                var values = item.Mods.Skip(i).Take(consumed).SelectMany(m => m.Values).Select(v => Math.Abs(v.Value)).ToArray();
                var key = (match.Id, scope);
                if (totals.TryGetValue(key, out var previous) && previous.Values.Length == values.Length)
                {
                    values = values.Select((v, index) => v + previous.Values[index]).ToArray();
                    totals[key] = (previous.Index, values);
                    rows[previous.Index] = new(match.Id, Render(match.Text, values), scope);
                }
                else
                {
                    totals[key] = (rows.Count, values);
                    rows.Add(new(match.Id, Render(match.Text, values), scope));
                }
            }
            i += consumed;
        }
        return new(item.Name, item.MapQuantity, item.MapRarity, item.MapPackSize, rows);
    }
    private static string Render(string text, decimal[] values)
    {
        var value = string.Join('/', values.Distinct().Select(v => v.ToString(CultureInfo.InvariantCulture)));
        if (value.Length == 0) return text;
        var percent = text.IndexOf('%');
        return percent >= 0 ? text.Insert(percent, value) : text + value;
    }

}
