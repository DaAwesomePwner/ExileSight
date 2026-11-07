// Source: Lailloken/Exile-UI data/english/map-info.txt (MIT). Ratings are user data, never packed.
using System.Text;
using ExileSight.Core.DataPacks;

namespace ExileSight.DataGen;

public static class MapModsConverter
{
    public static MapModsContent Read(byte[] bytes)
    {
        var entries = new Dictionary<string, MapModEntry>(StringComparer.Ordinal);
        string? pattern = null;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Finish()
        {
            if (!fields.ContainsKey("ID") && !fields.ContainsKey("type") && !fields.ContainsKey("text")) return;
            if (pattern is null || !fields.TryGetValue("ID", out var id) || !int.TryParse(id, out var number) || number < 0 ||
                !fields.TryGetValue("type", out var type) || string.IsNullOrWhiteSpace(type) ||
                !fields.TryGetValue("text", out var text) || string.IsNullOrWhiteSpace(text) || pattern.Split('|').Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("Invalid map modifier section: " + pattern);
            var key = id + "\0" + type + "\0" + text;
            if (entries.TryGetValue(key, out var previous))
            {
                entries[key] = previous with { Patterns = [.. previous.Patterns, pattern] };
            }
            else entries.Add(key, new(id, type, text, [pattern]));
        }
        foreach (var raw in Encoding.UTF8.GetString(bytes).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                Finish(); fields.Clear(); pattern = line[1..^1];
            }
            else
            {
                var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2) fields[parts[0]] = parts[1];
            }
        }
        Finish();
        if (entries.Count == 0) throw new InvalidOperationException("No map modifiers.");
        return new(entries.Values.OrderBy(x => int.Parse(x.Id, System.Globalization.CultureInfo.InvariantCulture)).ToArray());
    }
}
