using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Items;
using ExileSight.Core.DataPacks;

namespace ExileSight.Features.ItemInfo;

// Rule precedence ported from Exile-UI modules/item-checker.ahk:43–75,2102–2126,2177–2193 (MIT).
public sealed record HighlightRule(string Text, Highlight Match, string? ItemClass = null, bool Implicit = false, bool Enchant = false);
public sealed record HighlightProfile(string Name, HighlightRule[] Rules);
public sealed record ItemInfoSettings(HighlightProfile[] Profiles, int ActiveProfile = 0, string[]? InvertedLines = null,
    bool ShowCompare = true, bool ShowDefenceBasePercentile = true,
    bool ShowItemLevelColumn = true, bool ShowImplicits = true)
{
    public static ItemInfoSettings Default => new([]);
    public ItemInfoSettings Validate()
    {
        if (Profiles is null || Profiles.Length > 5 || ActiveProfile < 0 ||
            (Profiles.Length > 0 && ActiveProfile >= Profiles.Length) ||
            Profiles.Any(p => p is null || string.IsNullOrWhiteSpace(p.Name) || p.Rules is null ||
                p.Rules.Any(r => r is null || string.IsNullOrWhiteSpace(r.Text) || !Enum.IsDefined(r.Match))))
            throw new ArgumentException("Provide up to five valid highlight profiles and an active profile index.");
        return this;
    }
    public Highlight Evaluate(ParsedItem item, ModLine line)
    {
        if (Profiles.Length == 0) return Highlight.None;
        var rules = Profiles[ActiveProfile].Rules.Where(r => r.Enchant == line.Kind.HasFlag(ModKind.Enchant) &&
            r.Implicit == line.Kind.HasFlag(ModKind.Implicit));
        var text = ModRuleKey.Normalize(line.Text);
        return rules.Where(r => r.ItemClass == item.ItemClass).Concat(rules.Where(r => r.ItemClass is null))
            .FirstOrDefault(r => text == ModRuleKey.Normalize(r.Text))?.Match ?? Highlight.None;
    }
    public static ItemInfoSettings Load(IFeatureContext context)
    {
        try { return context.ReadSetting("profiles")?.Deserialize<ItemInfoSettings>()?.Validate() ?? Default; }
        catch (Exception e) when (e is JsonException or ArgumentException) { return Default; }
    }
    public void Save(IFeatureContext context) => context.WriteSetting("profiles", JsonSerializer.SerializeToElement(Validate()));
}
