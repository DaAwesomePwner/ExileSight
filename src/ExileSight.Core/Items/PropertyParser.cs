using System.Globalization;
using System.Text.RegularExpressions;

namespace ExileSight.Core.Items;

internal static class PropertyParser
{
    internal static bool Parse(ParsedItem item, string line, bool requirements, List<ParseWarning> warnings)
    {
        var colon = line.IndexOf(':');
        if (colon < 0) return false;
        var key = line[..colon];
        var text = line[(colon + 1)..].Trim();
        if (key == "Sockets")
        {
            item.SocketGroups.AddRange(text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return true;
        }
        if (!requirements && !EnglishItemPatterns.PropertyNames.Contains(key) && !key.StartsWith("Quality (", StringComparison.Ordinal)) return false;
        var values = ModParser.Values(text);
        var value = values.FirstOrDefault()?.Value;
        int? integer = value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
        if (values.Count == 0) warnings.Add(new(WarningSeverity.Warning, $"No numeric value in property: {line}"));
        if (requirements)
        {
            if (integer is { } n) item.Requirements[key] = n;
            return true;
        }
        item.Properties[key] = new(text, values);
        if (key.StartsWith("Quality", StringComparison.Ordinal)) item.Quality = value;
        switch (key)
        {
            case "Item Level": item.ItemLevel = integer; break;
            case "Level" when item.Rarity == ItemRarity.Gem: item.GemLevel = integer; break;
            case "Map Tier": item.MapTier = integer; break;
            case "Item Quantity": item.MapQuantity = value; break;
            case "Item Rarity": item.MapRarity = value; break;
            case "Monster Pack Size": item.MapPackSize = value; break;
            case "Stack Size": item.StackSize = Stack(text, warnings); break;
            case "Experience": item.GemExperience = Stack(text, warnings); break;
        }
        return true;
    }

    private static ItemStack? Stack(string text, List<ParseWarning> warnings)
    {
        var parts = text.Split('/');
        if (parts.Length == 2 && long.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var current)
            && long.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var maximum)) return new(current, maximum);
        warnings.Add(new(WarningSeverity.Warning, $"Invalid stack or experience: {text}"));
        return null;
    }

    internal static bool Metadata(ParsedItem item, string line, List<ParseWarning> warnings)
    {
        switch (line)
        {
            case "Corrupted": item.Corrupted = true; return true;
            case "Mirrored": item.Mirrored = true; return true;
            case "Unidentified": item.Unidentified = true; return true;
            case "Fractured Item": item.Fractured = true; return true;
            case "Synthesised Item": item.Synthesised = true; return true;
            case "Split": item.Split = true; return true;
            case "Unmodifiable": item.Unmodifiable = true; return true;
        }
        foreach (var influence in EnglishItemPatterns.Influences)
            if (line == influence + " Item")
            {
                if (!item.Influences.Contains(influence)) item.Influences.Add(influence);
                return true;
            }
        if (!line.StartsWith("Note: ", StringComparison.Ordinal) && !line.StartsWith('~')) return false;
        item.Note = line.StartsWith("Note: ", StringComparison.Ordinal) ? line[6..] : line;
        var match = Regex.Match(item.Note, @"^(~b/o|~price) (\d+(?:\.\d+)?)(?:/(\d+(?:\.\d+)?))? (\S+)$");
        var amount = ModParser.NumberValue(match.Groups[2].Value);
        var denominator = match.Groups[3].Success ? ModParser.NumberValue(match.Groups[3].Value) : 1;
        if (match.Success && amount.HasValue && denominator is > 0)
        {
            try { item.Price = new(item.Note, match.Groups[1].Value, amount.Value / denominator.Value, match.Groups[4].Value); }
            catch (OverflowException) { warnings.Add(new(WarningSeverity.Warning, "Price exceeds decimal range")); }
        }
        else if (item.Note.StartsWith("~b/o", StringComparison.Ordinal) || item.Note.StartsWith("~price", StringComparison.Ordinal))
            warnings.Add(new(WarningSeverity.Warning, "Invalid price note"));
        return true;
    }
}
