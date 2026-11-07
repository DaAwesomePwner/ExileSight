namespace ExileSight.Core.Items;

internal static class SectionParser
{
    internal static void Parse(ParsedItem item, List<string> lines, List<ParseWarning> warnings, ref int group, bool metadataSection)
    {
        if (lines.Count == 0) return;
        string? header = null;
        var requirements = lines[0] == "Requirements:";
        var properties = requirements || PropertyParser.IsPropertyStart(lines[0]) ||
            EnglishItemPatterns.WeaponTypes.Contains(lines[0]) ||
            (item.Rarity == ItemRarity.Gem && lines.Any(l => l.StartsWith("Level: ", StringComparison.Ordinal)));
        var flavour = lines[0].StartsWith('"') ||
            (item.Rarity is ItemRarity.Unique or ItemRarity.DivinationCard &&
             lines.All(l => l.EndsWith('.') || l.EndsWith('"')));
        var description = item.Rarity is ItemRarity.Gem or ItemRarity.Currency or ItemRarity.DivinationCard;
        foreach (var line in lines)
        {
            if (line == "Requirements:") continue;
            if (metadataSection && (item.Rarity != ItemRarity.DivinationCard || line.StartsWith("Note: ", StringComparison.Ordinal)) &&
                PropertyParser.Metadata(item, line, warnings)) continue;
            if (line.StartsWith('{'))
            {
                header = line;
                group++;
                if (!line.EndsWith('}')) warnings.Add(new(WarningSeverity.Warning, $"Incomplete modifier header: {line}"));
                continue;
            }
            if (properties && header is null)
            {
                PropertyParser.Parse(item, line, requirements, warnings);
                continue;
            }
            if (flavour) { item.FlavourText.Add(line); continue; }
            var mod = ModParser.Parse(line, header, header is null ? null : group);
            if (EnglishItemPatterns.Instructions.Any(p => line.StartsWith(p, StringComparison.Ordinal)) ||
                (description && header is null && mod.Kind == ModKind.Explicit))
            {
                item.OtherText.Add(line);
                continue;
            }
            // Once in a modifier section, text need not contain digits, or even punctuation.
            item.Mods.Add(mod);
            if (mod.Kind.HasFlag(ModKind.Enchant) && line.StartsWith("Allocates ", StringComparison.Ordinal))
                item.Anointment = line[10..].Replace(" (enchant)", "", StringComparison.Ordinal);
        }
    }
}
