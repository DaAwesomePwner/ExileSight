using System.Text.Json;

namespace ExileSight.Features.Timers;

internal sealed record TimerDocument(int SchemaVersion, TimerState[] Timers)
{
    internal static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly HashSet<string> Fields = ["id", "name", "kind", "duration", "savedValue", "endUtc", "runningSinceUtc", "alert", "alerted"];
    internal static (TimerState[] Rows, bool ReadOnly) Read(JsonElement root)
    {
        var legacy = root.ValueKind == JsonValueKind.Array;
        var readOnly = false;
        JsonElement timers;
        if (legacy) timers = root;
        else
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetDecimal(out var schema) || schema < 1) throw new JsonException("Invalid timer schema.");
            if (schema > 1) return ([], true);
            readOnly = root.EnumerateObject().Any(p => p.Name is not ("schemaVersion" or "timers"));
            if (!root.TryGetProperty("timers", out timers))
            {
                if (readOnly) return ([], true);
                throw new JsonException("Missing timers.");
            }
        }
        if (timers.ValueKind == JsonValueKind.Array)
            readOnly |= timers.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object &&
                row.EnumerateObject().Any(p => legacy ? p.Name != "IsRunning" && !Fields.Contains(JsonNamingPolicy.CamelCase.ConvertName(p.Name))
                    : !Fields.Contains(p.Name)));
        try
        {
            var rows = timers.Deserialize<TimerState[]>(legacy ? null : Options);
            if (!Valid(rows)) throw new JsonException("Invalid timers.");
            return (rows!, readOnly);
        }
        catch (JsonException) when (readOnly) { return ([], true); }
    }
    internal static bool Valid(TimerState[]? values) => values is not null && values.Length <= TimerStore.MaxTimers &&
        values.All(r => r is not null && r.IsValid()) && values.Select(r => r.Id).Distinct().Count() == values.Length;
}
