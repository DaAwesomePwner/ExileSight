using System.Text.Json;

namespace ExileSight.Features.SearchStrings;

public sealed record SavedSearch(string Name, string Text, bool Regex = false);

// Behaviour reference: Exile-UI modules/search-strings.ahk (named lists and multi-line cycling).
// Defaults below are independently authored, not copied from Exile-UI.
public static class SearchSettings
{
    public static Dictionary<string, SavedSearch[]> Load(JsonElement? saved)
    {
        var result = new Dictionary<string, SavedSearch[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Vendor"] = [new("Movement speed boots", "movement speed"),
                new("Chromatic / RGB links", "r-g-b|r-b-g|g-r-b|g-b-r|b-r-g|b-g-r", true)],
            ["Stash"] = [new("Currency", "currency"), new("Maps", "map")]
        };
        if (saved is null) return result;
        var users = JsonSerializer.Deserialize<Dictionary<string, SavedSearch[]>>(saved.Value,
            new JsonSerializerOptions { RespectRequiredConstructorParameters = true })
            ?? throw new ArgumentException("Expected vendor lists");
        if (users.Count > 50) throw new ArgumentException("Too many vendors");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (vendor, entries) in users)
        {
            ValidateName(vendor);
            if (!names.Add(vendor) || entries is null || entries.Length > 100)
                throw new ArgumentException("Invalid vendor list");
            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                Compile(entry);
                if (!labels.Add(entry.Name)) throw new ArgumentException("Duplicate search name");
            }
            result[vendor] = entries; // A supplied list is authoritative, including removals.
        }
        return result;
    }
    public static string[] Compile(SavedSearch entry)
    {
        if (entry is null) throw new ArgumentException("Missing search");
        ValidateName(entry.Name);
        if (entry.Text is null || entry.Text.Length > 25000) throw new ArgumentException("Invalid search text");
        var lines = entry.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length > 100) throw new ArgumentException("Too many lines");
        return lines.Select(line =>
        {
            if (string.IsNullOrWhiteSpace(line) || line.Any(char.IsControl) || line.Contains('"'))
                throw new ArgumentException("Invalid search line");
            var text = "\"" + (entry.Regex ? line : string.Concat(line.Select(c =>
                @"\.^$|?*+()[]{}".Contains(c) ? "\\" + c : c.ToString()))) + "\"";
            if (text.Length > 250) throw new ArgumentException("Search exceeds 250 characters after escaping");
            return text;
        }).ToArray();
    }
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl))
            throw new ArgumentException("Invalid name");
    }
}
