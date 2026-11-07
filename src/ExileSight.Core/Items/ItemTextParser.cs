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
        for (var index = headerIndex + 1; index < sections.Count; index++)
        {
            var section = sections[index];
            // Flags are trailing sections, never words inside properties, rewards or flavour.
            var metadata = section.All(PropertyParser.IsMetadataLine) &&
                sections.Skip(index + 1).All(s => s.All(l => PropertyParser.IsMetadataLine(l) ||
                    EnglishItemPatterns.Instructions.Any(p => l.StartsWith(p, StringComparison.Ordinal))));
            SectionParser.Parse(item, section, warnings, ref group, metadata);
        }
        if (item.Unidentified && item.BaseType is null) item.BaseType = item.Name;
        item.Fractured |= item.FracturedMods.Any();
        ResolveMagicBase(item);
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
        item.Name = NormalizeName(names.FirstOrDefault() ?? "", item);
        item.BaseType = names.Length > 1 ? NormalizeName(names[1], item) : item.Rarity == ItemRarity.Magic ? null : item.Name;
        return item;
    }

    // Presentation-prefix behaviour follows Exile-UI modules/item-checker.ahk:192-194 (MIT).
    private static string NormalizeName(string name, ParsedItem item)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var prefix in EnglishItemPatterns.NamePrefixes)
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    item.Synthesised |= prefix == "Synthesised ";
                    name = name[prefix.Length..];
                    changed = true;
                }
        } while (changed);
        return name;
    }

    private static void ResolveMagicBase(ParsedItem item)
    {
        if (item.Rarity != ItemRarity.Magic || item.BaseType is not null) return;
        var name = item.Name;
        var affixes = item.Mods.Where(m => m.AffixName is not null).DistinctBy(m => m.Group).ToArray();
        foreach (var mod in affixes)
        {
            if (mod.AffixType == AffixType.Prefix && name.StartsWith(mod.AffixName + " ", StringComparison.Ordinal))
                name = name[(mod.AffixName!.Length + 1)..];
            if (mod.AffixType == AffixType.Suffix && name.EndsWith(" " + mod.AffixName, StringComparison.Ordinal))
                name = name[..^(mod.AffixName!.Length + 1)];
        }
        if (name.Length > 0 && name != item.Name) item.BaseType = NormalizeName(name, item);
    }
}
