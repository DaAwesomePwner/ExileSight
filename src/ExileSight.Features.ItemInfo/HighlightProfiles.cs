using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Items;
using ExileSight.Core.DataPacks;

namespace ExileSight.Features.ItemInfo;

// Rule precedence ported from Exile-UI modules/item-checker.ahk:43–75,2102–2126,2177–2193 (MIT).
public sealed record HighlightRule(string Text, Highlight Match, string? ItemClass = null, bool Implicit = false);
public sealed record HighlightProfile(string Name, HighlightRule[] Rules);
public sealed record ItemInfoSettings(HighlightProfile[] Profiles, int ActiveProfile = 0, string[]? InvertedLines = null)
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
        var rules = Profiles[ActiveProfile].Rules.Where(r => r.Implicit == line.Kind.HasFlag(ModKind.Implicit));
        var text = StatText.StripCopySuffixes(line.Text);
        return rules.Where(r => r.ItemClass == item.ItemClass).Concat(rules.Where(r => r.ItemClass is null))
            .FirstOrDefault(r => text.Contains(r.Text, StringComparison.OrdinalIgnoreCase))?.Match ?? Highlight.None;
    }
    public static ItemInfoSettings Load(IFeatureContext context)
    {
        try { return context.ReadSetting("profiles")?.Deserialize<ItemInfoSettings>()?.Validate() ?? Default; }
        catch (Exception e) when (e is JsonException or ArgumentException) { return Default; }
    }
    public void Save(IFeatureContext context) => context.WriteSetting("profiles", JsonSerializer.SerializeToElement(Validate()));
}
