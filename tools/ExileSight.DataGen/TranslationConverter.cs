using System.Text.Json;
using ExileSight.Core.DataPacks;

namespace ExileSight.DataGen;

internal static class TranslationConverter
{
    public static StatTranslation[] Read(byte[] json, ModEntry[] mods)
    {
        using var document = JsonDocument.Parse(json);
        var ids = mods.SelectMany(m => m.Stats).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        return document.RootElement.EnumerateArray().Select((entry, index) => (entry, index))
            .Where(pair => pair.entry.GetProperty("ids").EnumerateArray().Any(id => ids.Contains(id.GetString()!)))
            .Select(pair => new StatTranslation("translation-" + pair.index,
                Strings(pair.entry.GetProperty("ids")),
                pair.entry.GetProperty("English").EnumerateArray().Select(ReadVariant).ToArray(),
                pair.entry.TryGetProperty("hidden", out var hidden) && hidden.ValueKind == JsonValueKind.True)).ToArray();
    }

    private static StatVariant ReadVariant(JsonElement entry)
    {
        var conditions = entry.TryGetProperty("condition", out var singular) ? singular : entry.GetProperty("conditions");
        var formats = entry.TryGetProperty("format", out singular) ? singular : entry.GetProperty("formats");
        return new(entry.GetProperty("string").GetString()!,
            conditions.EnumerateArray().Select(c => new StatCondition(Number(c, "min"), Number(c, "max"),
                c.TryGetProperty("negated", out var n) && n.ValueKind == JsonValueKind.True)).ToArray(),
            Strings(formats), entry.GetProperty("index_handlers").EnumerateArray().Select(Strings).ToArray());
    }
    private static decimal? Number(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
    private static string[] Strings(JsonElement e) => e.EnumerateArray().Select(v => v.GetString()!).ToArray();
}
