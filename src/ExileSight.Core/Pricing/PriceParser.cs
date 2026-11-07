using System.Text.Json;

namespace ExileSight.Core.Pricing;

/// <summary>Exchange field behavior follows Exile-UI modules/stash-ninja.ahk; legacy overviews are also accepted.</summary>
public static class PriceParser
{
    public static PriceOverview Parse(string json, string category)
    {
        try { return ParseCore(json, category); }
        catch (InvalidOperationException e) { throw new JsonException("Invalid price overview shape.", e); }
    }
    private static PriceOverview ParseCore(string json, string category)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array)
            throw new JsonException("Price overview has no lines.");
        var metadata = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (root.TryGetProperty("items", out var items))
            foreach (var item in items.EnumerateArray())
                if (Text(item, "id") is { } id) metadata[id] = item;
        decimal? chaosPerPrimary = 1, divinePerPrimary = null;
        if (root.TryGetProperty("core", out var core))
        {
            var primary = Text(core, "primary");
            core.TryGetProperty("rates", out var rates);
            chaosPerPrimary = primary == "chaos" ? 1 : Number(rates, "chaos");
            divinePerPrimary = primary == "divine" ? 1 : Number(rates, "divine");
        }
        var prices = new List<ItemPrice>();
        foreach (var line in lines.EnumerateArray())
        {
            var id = Text(line, "id") ?? Text(line, "detailsId") ?? Text(line, "currencyTypeName");
            if (id is null) continue;
            metadata.TryGetValue(id, out var meta);
            var name = Text(meta, "name") ?? Text(line, "name") ?? Text(line, "currencyTypeName") ?? id;
            var chaos = Number(line, "chaosValue") ?? Number(line, "chaosEquivalent") ??
                Number(line, "primaryValue") * chaosPerPrimary;
            if (chaos is null or < 0) continue;
            var history = new List<decimal?>();
            foreach (var key in new[] { "sparkline", "sparkLine", "receiveSparkLine", "lowConfidenceSparkline", "lowConfidenceSparkLine" })
            {
                if (!line.TryGetProperty(key, out var spark) || spark.ValueKind != JsonValueKind.Object ||
                    !spark.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) continue;
                history.AddRange(data.EnumerateArray().TakeLast(7).Select(v => v.TryDecimal()));
                break;
            }
            var count = Number(line, "listingCount") ?? Number(line, "count");
            prices.Add(new(id, name, category, Text(line, "baseType") ?? Text(meta, "baseType"),
                chaos.Value, Number(line, "divineValue") ?? Number(line, "primaryValue") * divinePerPrimary,
                history.AsReadOnly(), count is >= 0 and <= int.MaxValue ? (int)count : null, Text(line, "confidence"),
                Number(line, "links") is >= 0 and <= 6 ? (int?)Number(line, "links") : null, Text(line, "variant")));
        }
        var divine = prices.FirstOrDefault(p => p.Name == "Divine Orb" || p.Id == "divine")?.ChaosValue;
        divine ??= divinePerPrimary is > 0 ? chaosPerPrimary / divinePerPrimary : null;
        divine ??= prices.Where(p => p.DivineValue is > 0 && p.ChaosValue > 0)
            .Select(p => (decimal?)(p.ChaosValue / p.DivineValue!.Value)).FirstOrDefault();
        if (divine is <= 0) divine = null;
        return new(prices.Select(p => p with { DivineValue = divine is > 0 ? p.ChaosValue / divine : p.DivineValue }).ToArray(), divine);
    }
    private static string? Text(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) &&
        value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.ToString() : null;
    private static decimal? Number(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) ? value.TryDecimal() : null;
    private static decimal? TryDecimal(this JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : null;
}
