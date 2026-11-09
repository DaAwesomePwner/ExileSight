using System.Globalization;
using System.Text.Json;

namespace ExileSight.Features.Lab;

// Independently implemented from the public LabCompass format documentation; no upstream code/data copied.
public static class LabImport
{
    public const int MaxBytes = 1024 * 1024;
    public static IReadOnlyList<string> Difficulties { get; } = Array.AsReadOnly(new[] { "Normal", "Cruel", "Merciless", "Uber" });
    public static LabLayout Parse(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new ArgumentException("Lab JSON exceeds the 1 MB limit.");
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            var difficulty = Text(root, "difficulty");
            if (!Difficulties.Contains(difficulty)) throw new ArgumentException("Unknown lab difficulty.");
            if (!DateOnly.TryParseExact(Text(root, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)) throw new ArgumentException("Date must be yyyy-MM-dd.");
            var rooms = root.GetProperty("rooms").EnumerateArray().Select(ReadRoom).ToArray();
            if (rooms.Length is 0 or > 512) throw new ArgumentException("Layout must contain 1–512 rooms.");
            var ids = rooms.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
            if (ids.Count != rooms.Length) throw new ArgumentException("Room IDs must be unique.");
            foreach (var room in rooms)
                if (room.Exits.Values.Any(id => !ids.Contains(id)) ||
                    room.SecretPassage is { Length: > 0 } secret && !ids.Contains(secret))
                    throw new ArgumentException("Room links must reference existing room IDs.");
            return new(difficulty, date, Text(root, "weapon"), Text(root, "phase1"), Text(root, "phase2"),
                Text(root, "trap1"), Text(root, "trap2"),
                Enumerable.Range(1, 3).Select(i => Optional(root, $"section{i}layout") ?? "").ToArray(), rooms);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new ArgumentException("Invalid lab JSON: missing field or incorrect field type.", e); }
    }
    private static LabRoom ReadRoom(JsonElement room) => new(Text(room, "id"), Text(room, "name"), Text(room, "areacode"),
        Coordinate(room, "x"), Coordinate(room, "y"), room.GetProperty("contents").EnumerateArray()
            .Select(c => c.GetString() ?? throw new ArgumentException("Contents must be strings.")).ToArray(),
        room.GetProperty("exits").EnumerateObject().ToDictionary(p => p.Name,
            p => p.Value.GetString() ?? throw new ArgumentException("Exit IDs must be strings.")),
        Optional(room, "secretPassage") ?? Optional(room, "secret_passage"));
    private static string Text(JsonElement element, string key) =>
        element.GetProperty(key).GetString() is { Length: > 0 } value ? value : throw new ArgumentException($"{key} is required.");
    private static string? Optional(JsonElement element, string key) => element.TryGetProperty(key, out var value) ? value.GetString() : null;
    private static double Coordinate(JsonElement room, string key)
    {
        var raw = room.GetProperty(key);
        if (!double.TryParse(raw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !double.IsFinite(value) || Math.Abs(value) > 100000) throw new ArgumentException($"Invalid coordinate: {key}.");
        return value;
    }
}
