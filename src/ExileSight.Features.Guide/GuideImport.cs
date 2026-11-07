// JSON format: Exile-UI default guide (MIT), derived from HeartofPhos/exile-leveling (MIT).
using System.Text.Json;
using System.Text.RegularExpressions;
using ExileSight.Core.DataPacks;

namespace ExileSight.Features.Guide;

public static class GuideImport
{
    public const int MaxBytes = 1024 * 1024;
    public static GuideData Read(byte[] bytes, GuideData pack)
    {
        if (bytes.Length > MaxBytes) throw new ArgumentException("Guide exceeds 1 MiB.");
        try { return Parse(bytes, pack); }
        catch (Exception e) when (e is RegexMatchTimeoutException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new ArgumentException("Invalid guide schema or references.", e); }
    }
    private static GuideData Parse(byte[] bytes, GuideData pack)
    {
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 12 });
        var pages = new List<GuidePageData>();
        var areas = pack.Areas.ToDictionary(a => a.Id);
        var quests = pack.Gems.Where(g => g.QuestReward is not null).Select(g => g.QuestReward!.QuestId).ToHashSet();
        // Existing pack placeholders include non-gem reward/display hints; no arbitrary reference is admitted.
        var hints = pack.Pages.SelectMany(p => p.Lines).SelectMany(l => Tokens(l.Text, "<([^>]+)>")).ToHashSet();
        foreach (var act in json.RootElement.EnumerateArray())
        {
            var chapter = new List<GuidePageData>();
            foreach (var page in act.EnumerateArray())
            {
                GuideConditionData? condition = null;
                var lines = page;
                if (page.ValueKind == JsonValueKind.Object)
                {
                    if (page.EnumerateObject().Any(p => p.Name is not ("condition" or "lines"))) throw new JsonException();
                    var c = page.GetProperty("condition");
                    if (c.GetArrayLength() != 2) throw new JsonException();
                    var kind = c[0].GetString()!;
                    var values = c[1].ValueKind == JsonValueKind.String ? new[] { c[1].GetString()! } : c[1].EnumerateArray().Select(x => x.GetString()!).ToArray();
                    if (values.Length == 0 || values.Any(v => kind switch
                        { "league-start" => v is not ("yes" or "no"), "bandit" => v is not ("none" or "oak" or "alira" or "kraityn"), _ => true })) throw new JsonException();
                    condition = new(kind, values); lines = page.GetProperty("lines");
                }
                var result = lines.EnumerateArray().Select(l =>
                {
                    var text = l.GetString() ?? throw new JsonException();
                    if (text.Length > 4096 || text.Any(c => char.IsControl(c) && c != '\t')) throw new JsonException();
                    var ids = Tokens(text, @"areaid([A-Za-z0-9_]+)");
                    var refs = Tokens(text, "<([^>]+)>");
                    if (ids.Any(id => !areas.ContainsKey(id)) || refs.Any(r => !hints.Contains(r) && !quests.Contains(Regex.Replace(r, "[0-9]+$", "")))) throw new JsonException();
                    return new GuideLineData(text, ids, [], Tokens(text, @"\(lvl:([^)]+)\)"));
                }).ToArray();
                if (result.Length is < 1 or > 100 || pages.Count + chapter.Count >= 2000) throw new JsonException();
                chapter.Add(new(0, chapter.Count + 1, condition, result));
            }
            var acts = chapter.SelectMany(p => p.Lines).SelectMany(l => l.AreaIds).Select(id => areas[id].Act)
                .GroupBy(a => a).OrderByDescending(g => g.Count()).ToArray();
            if (acts.Length == 0 || acts[0].Key is < 1 or > 10 || acts.Length > 1 && acts[0].Count() == acts[1].Count()) throw new JsonException();
            pages.AddRange(chapter.Select(p => p with { Act = acts[0].Key }));
        }
        if (pages.Count == 0) throw new JsonException();
        return new(pages, pack.Areas, pack.Gems);
    }
    private static string[] Tokens(string text, string pattern) => Regex.Matches(text, pattern,
        RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100)).Select(m => m.Groups[1].Value).ToArray();
}
