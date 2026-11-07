using System.Text.Json;
using ExileSight.Core.DataPacks;

namespace ExileSight.DataGen;

/// <summary>Endpoint probe, not a claim about every possible in-game roll or actual client recordings.</summary>
public static class TranslationCoverage
{
    public static async Task WriteAsync(string path, DataPack[] packs)
    {
        var translations = packs.SelectMany(p => p.Content.Translations ?? []).ToDictionary(t => t.Id);
        var mods = packs.SelectMany(p => p.Content.Mods).ToArray();
        var total = 0;
        var unmatched = 0;
        var missingStats = 0;
        foreach (var mod in mods)
        {
            var referenced = mod.TextTemplateIds.Select(id => translations[id]).Where(t => !t.Hidden).ToArray();
            missingStats += mod.Stats.Count(s => !referenced.Any(t => t.StatIds.Contains(s.Id)));
            foreach (var translation in referenced)
            foreach (var maximum in new[] { false, true })
            {
                var raw = translation.StatIds.Select(id => mod.Stats.FirstOrDefault(s => s.Id == id))
                    .Select(s => s is null ? 0m : maximum ? s.Max : s.Min).ToArray();
                var variant = translation.English.FirstOrDefault(v => v.Conditions.Length == raw.Length &&
                    raw.Select((value, i) => v.Conditions[i].Accepts(value)).All(b => b));
                if (variant is null) continue;
                var count = variant.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                total += count;
                var rendered = StatText.Render(translation, raw);
                if (rendered is null) { unmatched += count; continue; }
                foreach (var line in rendered.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var matches = StatText.Match(line.TrimEnd('\r'), [translation]);
                    if (!matches.Any(m => m.Values.All(v =>
                        raw[Array.IndexOf(translation.StatIds, v.StatId)] >= v.Min &&
                        raw[Array.IndexOf(translation.StatIds, v.StatId)] <= v.Max))) unmatched++;
                }
            }
        }
        // Anonymous serialization is confined to the build-time tool, not the app.
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            sourceSha = packs[0].Manifest.Source.Sha,
            metric = "Reachable non-hidden English template lines at each mod's min/max endpoints; repeated mod lines count separately",
            totalLines = total, unmatchedLines = unmatched,
            unmatchedPercent = total == 0 ? 0 : Math.Round(100m * unmatched / total, 4),
            missingTranslationStatOccurrences = missingStats
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
