using ExileSight.Core.Games;

namespace ExileSight.Core.Items;

/// <summary>Portable English PoE1 clipboard parser. Unknown text is retained, never matched to a data pack.</summary>
public static class ItemTextParser
{
    public static ItemParseResult Parse(string? text, Game game)
    {
        List<ParseWarning> warnings = [];
        if (game != Game.PoE1) return new(null, [new(WarningSeverity.Error, "Only English PoE1 item text is supported")]);
        if (string.IsNullOrWhiteSpace(text)) return new(null, [new(WarningSeverity.Error, "Item text is empty")]);
        var sections = new List<List<string>> { new() };
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim().TrimStart('\uFEFF');
            if (line == EnglishItemPatterns.Separator) sections.Add([]);
            else if (line.Length > 0) sections[^1].Add(line);
        }
        // Equipped-but-unusable copies may have a warning section before the item header.
        var headerIndex = sections.FindIndex(s => s.Any(l => l.StartsWith(EnglishItemPatterns.Rarity, StringComparison.Ordinal)));
        if (headerIndex < 0) return new(null, [new(WarningSeverity.Error, "Missing rarity header")]);
        var item = Header(sections[headerIndex], game, warnings);
        if (item.Name.Length == 0) warnings.Add(new(WarningSeverity.Error, "Missing item name"));
        var group = 0;
        foreach (var section in sections.Skip(headerIndex + 1)) ParseSection(item, section, warnings, ref group);
        if (item.Unidentified && item.BaseType is null) item.BaseType = item.Name;
        item.Fractured |= item.FracturedMods.Any();
        item.Synthesised |= item.BaseType?.StartsWith("Synthesised ", StringComparison.Ordinal) == true;
        return new(item, warnings);
    }

    private static ParsedItem Header(List<string> lines, Game game, List<ParseWarning> warnings)
    {
        var item = new ParsedItem { Game = game };
        var classLine = lines.FirstOrDefault(l => l.StartsWith(EnglishItemPatterns.ItemClass, StringComparison.Ordinal));
        item.ItemClass = classLine?[EnglishItemPatterns.ItemClass.Length..] ?? "";
        if (classLine is null) warnings.Add(new(WarningSeverity.Warning, "Missing item class"));
        var index = lines.FindIndex(l => l.StartsWith(EnglishItemPatterns.Rarity, StringComparison.Ordinal));
        var rarity = lines[index][EnglishItemPatterns.Rarity.Length..].Replace(" ", "", StringComparison.Ordinal);
        if (Enum.TryParse<ItemRarity>(rarity, out var parsed) && Enum.IsDefined(parsed)) item.Rarity = parsed;
        else warnings.Add(new(WarningSeverity.Warning, $"Unknown rarity: {rarity}"));
        var names = lines.Skip(index + 1).ToArray();
        item.Name = names.FirstOrDefault() ?? "";
        item.BaseType = names.Length > 1 ? names[1] : item.Rarity == ItemRarity.Magic ? null : item.Name;
        return item;
    }

    private static void ParseSection(ParsedItem item, List<string> lines, List<ParseWarning> warnings, ref int group)
    {
        string? header = null;
        var requirements = lines.FirstOrDefault() == "Requirements:";
        var flavour = lines.FirstOrDefault()?.StartsWith('"') == true;
        // A trailing unique prose section without numeric/marked stats is kept separately.
        flavour |= item.Rarity == ItemRarity.Unique && item.Mods.Count > 0 &&
            lines.All(l => !l.Contains(':') && !l.StartsWith('{') && !l.Any(char.IsDigit) &&
                !IsMetadata(l) && !LooksLikeMod(l) && !l.Contains("(implicit)", StringComparison.Ordinal));
        foreach (var line in lines)
        {
            if (line == "Requirements:") continue;
            if (PropertyParser.Metadata(item, line, warnings)) continue;
            if (line.StartsWith('{'))
            {
                header = line;
                group++;
                if (!line.EndsWith('}')) warnings.Add(new(WarningSeverity.Warning, $"Incomplete modifier header: {line}"));
                continue;
            }
            if (header is null && PropertyParser.Parse(item, line, requirements, warnings)) continue;
            if (flavour) { item.FlavourText.Add(line); continue; }
            var mod = ModParser.Parse(line, header, header is null ? null : group);
            if (header is not null || mod.Kind != ModKind.Explicit || LooksLikeMod(line))
            {
                item.Mods.Add(mod);
                if (mod.Kind.HasFlag(ModKind.Eater) && !item.Influences.Contains("Eater of Worlds")) item.Influences.Add("Eater of Worlds");
                if (mod.Kind.HasFlag(ModKind.Exarch) && !item.Influences.Contains("Searing Exarch")) item.Influences.Add("Searing Exarch");
                if (mod.Kind.HasFlag(ModKind.Enchant) && line.StartsWith("Allocates ", StringComparison.Ordinal))
                    item.Anointment = line[10..].Replace(" (enchant)", "", StringComparison.Ordinal);
            }
            else item.OtherText.Add(line);
        }
    }

    private static bool LooksLikeMod(string line) =>
        !line.StartsWith("Right click", StringComparison.Ordinal) && !line.StartsWith("Place ", StringComparison.Ordinal) &&
        (line.Any(char.IsDigit) || line.StartsWith("Allocates ", StringComparison.Ordinal) ||
         line.StartsWith("You ", StringComparison.Ordinal) || line.StartsWith("Cannot ", StringComparison.Ordinal));

    private static bool IsMetadata(string line) => line is "Corrupted" or "Mirrored" or "Unidentified" or "Split" or "Unmodifiable" || line.EndsWith(" Item", StringComparison.Ordinal);
}
