// Source format: Lailloken/Exile-UI data/english/[leveltracker] default guide.json (MIT),
// derived from HeartofPhos/exile-leveling (MIT). Areas and gems: pinned RePoE exports.
using System.Text.Json;
using System.Text.RegularExpressions;
using ExileSight.Core.DataPacks;

namespace ExileSight.DataGen;

public static class GuideConverter
{
    // Upstream display placeholders that are not gem quest references.
    private static readonly HashSet<string> DisplayHints = new(StringComparer.Ordinal)
    {
        "2_respecs", "amulet", "armor", "belt", "belt/amu", "boots", "fallen_from_grace", "helmet",
        "invite_to_hideout", "jewel", "respec_points", "respecs", "ring", "the_act-tracker_guide_ends_here",
        "the_way_forward", "type_\"/passives\"_in_chat", "weap", "weapon/offhand"
    };
    public static GuideContent Read(byte[] guide, byte[] areas, byte[] gems)
    {
        using var a = JsonDocument.Parse(areas);
        using var g = JsonDocument.Parse(gems);
        using var steps = JsonDocument.Parse(guide);
        var areaEntries = a.RootElement.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p =>
        {
            var v = p.Value;
            return new GuideArea(Text(v, "id"), Text(v, "name"), v.GetProperty("act").GetInt32(),
                v.GetProperty("area_level").GetInt32(), v.GetProperty("is_town").GetBoolean(),
                v.GetProperty("has_waypoint").GetBoolean(), Strings(v.GetProperty("connections")));
        }).ToArray();
        var gemEntries = g.RootElement.EnumerateObject().Where(p => p.Value.GetProperty("display_name").ValueKind == JsonValueKind.String).OrderBy(p => p.Name, StringComparer.Ordinal).Select(p =>
        {
            var v = p.Value;
            GemReward? reward = null;
            if (v.TryGetProperty("quest_reward", out var r) && r.ValueKind != JsonValueKind.Null)
                reward = new(r.GetProperty("act").GetInt32(), QuestId(Text(r, "quest")), Text(r, "quest"),
                    Strings(r.GetProperty("classes")).Distinct().ToArray());
            var levels = v.GetProperty("per_level").EnumerateObject().Select(l => new GemLevel(int.Parse(l.Name,
                System.Globalization.CultureInfo.InvariantCulture), l.Value.GetProperty("required_level").ValueKind == JsonValueKind.Number ? l.Value.GetProperty("required_level").GetInt32() : null,
                (l.Value.GetProperty("stat_requirements").ValueKind == JsonValueKind.Object
                    ? l.Value.GetProperty("stat_requirements").EnumerateObject().ToArray() : []).Where(s => s.Value.ValueKind == JsonValueKind.Number)
                    .ToDictionary(s => s.Name, s => s.Value.GetInt32()))).OrderBy(l => l.Level).ToArray();
            return new GuideGem(p.Name, Text(v, "display_name"), levels, reward);
        }).ToArray();
        var ids = areaEntries.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var quests = gemEntries.Where(x => x.QuestReward is not null).Select(x => x.QuestReward!.QuestId).ToHashSet();
        var pages = new List<GuidePage>();
        foreach (var chapter in steps.RootElement.EnumerateArray())
        {
            // Cross-act travel is expected. The dominant referenced area act identifies the chapter;
            // never assign acts from array position (custom guides may start later or reorder chapters).
            var chapterPages = new List<GuidePage>();
            var pageNumber = 0;
            foreach (var page in chapter.EnumerateArray())
            {
                GuideCondition? condition = null;
                var lines = page;
                if (page.ValueKind == JsonValueKind.Object)
                {
                    var c = page.GetProperty("condition");
                    var kind = c[0].GetString()!;
                    var values = c[1].ValueKind == JsonValueKind.Array ? Strings(c[1]) : [c[1].GetString()!];
                    if (c.GetArrayLength() != 2 || values.Length == 0 || kind switch
                        { "league-start" => values.Any(x => x is not ("yes" or "no")),
                          "bandit" => values.Any(x => x is not ("none" or "oak" or "alira" or "kraityn")), _ => true })
                        throw new InvalidOperationException("Invalid guide condition.");
                    condition = new(kind, values);
                    lines = page.GetProperty("lines");
                }
                var converted = Strings(lines).Select(line =>
                {
                    var references = Tokens(line, @"areaid([A-Za-z0-9_]+)");
                    if (references.Any(id => !ids.Contains(id))) throw new InvalidOperationException("Missing guide area reference.");
                    var placeholders = Tokens(line, "<([^>]+)>").Where(x => !DisplayHints.Contains(x))
                        .Select(x => Regex.Replace(x, "[0-9]+$", "")).Distinct().ToArray();
                    if (placeholders.Any(id => !quests.Contains(id))) throw new InvalidOperationException("Missing guide quest reference: " + line);
                    return new GuideLine(line, references, placeholders, Tokens(line, @"\(lvl:([^)]+)\)"));
                }).ToArray();
                if (converted.Length == 0) throw new InvalidOperationException("Empty guide page.");
                chapterPages.Add(new(0, ++pageNumber, condition, converted));
            }
            var acts = chapterPages.SelectMany(p => p.Lines).SelectMany(l => l.AreaIds)
                .Select(id => areaEntries.Single(a => a.Id == id).Act).GroupBy(act => act)
                .OrderByDescending(group => group.Count()).ToArray();
            if (acts.Length == 0 || (acts.Length > 1 && acts[0].Count() == acts[1].Count()))
                throw new InvalidOperationException("Missing or ambiguous guide chapter act references.");
            pages.AddRange(chapterPages.Select(p => p with { Act = acts[0].Key }));
        }
        if (pages.Count == 0) throw new InvalidOperationException("Empty guide.");
        return new(pages.ToArray(), areaEntries, gemEntries);
    }
    private static string QuestId(string name) => name.ToLowerInvariant().Replace(' ', '_');
    private static string Text(JsonElement v, string key) => v.GetProperty(key).GetString() ?? "";
    private static string[] Strings(JsonElement v) => v.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
    private static string[] Tokens(string line, string pattern) => Regex.Matches(line, pattern,
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(1))
        .Select(m => m.Groups[1].Value).Distinct().ToArray();
}
