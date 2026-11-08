using ExileSight.Core.DataPacks;
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo;

public interface IItemInfoData
{
    BaseEntry? FindBase(ParsedItem item);
    ModEntry? FindMod(ParsedItem item, IReadOnlyList<ModLine> group);
    StatMatch[] MatchLine(ModLine line);
    ModEntry? FindMod(ParsedItem item, IReadOnlyList<ModLine> group,
        IReadOnlyDictionary<ModLine, StatMatch[]> matches) => FindMod(item, group);
}

/// <summary>App injects already loaded packs; this adapter performs no IO.</summary>
public sealed class PackItemInfoData : IItemInfoData
{
    private readonly BaseEntry[] bases;
    private readonly ModEntry[] mods;
    private readonly StatTranslation[] stats;
    private readonly Dictionary<string, StatTranslation> translations;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, StatTranslation[][][]> fragments = new();

    public PackItemInfoData(PackContent? bases, PackContent? mods, PackContent? stats)
    {
        var compact = new CompactItemData();
        this.bases = compact.Bases(bases?.Bases ?? []);
        var domains = this.bases.Select(b => b.Domain).ToHashSet(StringComparer.Ordinal);
        // FindMod can only return prefix/suffix entries in a loaded base's domain.
        this.mods = compact.Mods((mods?.Mods ?? []).Where(m => domains.Contains(m.Domain) &&
            (m.AffixType.Equals("prefix", StringComparison.OrdinalIgnoreCase) ||
             m.AffixType.Equals("suffix", StringComparison.OrdinalIgnoreCase))).ToArray());
        this.stats = compact.Translations(stats?.Translations ?? []);
        translations = this.stats.ToDictionary(t => t.Id);
    }

    private bool Covers(string id, IReadOnlyList<ModLine> group)
    {
        if (!translations.ContainsKey(id)) return false;
        var variants = fragments.GetOrAdd(id, key =>
        {
            var t = translations[key];
            return t.English.Select(v => v.Text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
                    new[] { t with { English = [v with { Text = line + "\n" }] } }).ToArray()).ToArray();
        });
        return variants.Any(variant => variant.All(fragment => group.Any(line =>
            StatText.Match(StatText.StripCopySuffixes(line.Text), fragment).Length > 0)));
    }

    public BaseEntry? FindBase(ParsedItem item) => bases.FirstOrDefault(b =>
        ItemPackIdentity.ClassKey(b.ItemClass) == ItemPackIdentity.ClassKey(item.ItemClass) && b.Name == item.BaseType);

    // Exile-UI modules/item-checker.ahk:1436–1463 (MIT): name, category, type, all text and spawn tags; first match.
    public ModEntry? FindMod(ParsedItem item, IReadOnlyList<ModLine> group)
        => FindModCore(item, group, MatchLine);

    public ModEntry? FindMod(ParsedItem item, IReadOnlyList<ModLine> group,
        IReadOnlyDictionary<ModLine, StatMatch[]> perLine) => FindModCore(item, group, line => perLine[line]);

    private ModEntry? FindModCore(ParsedItem item, IReadOnlyList<ModLine> group, Func<ModLine, StatMatch[]> match)
    {
        var basis = FindBase(item);
        if (basis is null || group.Count == 0) return null;
        var first = group[0];
        if (first.AffixName is null || first.AffixType == AffixType.Unknown) return null;
        var matches = group.Select(match).ToArray();
        var tags = ItemPackIdentity.SpawnTags(item, basis);
        return mods.FirstOrDefault(m => m.Name == first.AffixName &&
            m.AffixType.Equals(first.AffixType.ToString(), StringComparison.OrdinalIgnoreCase) &&
            m.Domain == basis.Domain && (m.SpawnWeights.FirstOrDefault(w => tags.Contains(w.Tag))?.Weight ?? 0) > 0 &&
            m.TextTemplateIds.Length > 0 && matches.All(line => line.Any(match =>
                m.TextTemplateIds.Contains(match.TranslationId) && match.Values.All(v =>
                    m.Stats.Any(s => s.Id == v.StatId && v.Min <= s.Max && v.Max >= s.Min)))) &&
            m.TextTemplateIds.All(id => Covers(id, group)) &&
            m.Stats.All(s => matches.Any(line => line.Any(match => m.TextTemplateIds.Contains(match.TranslationId) &&
                match.Values.Any(v => v.StatId == s.Id)))));
    }

    public Task PrepareAsync(CancellationToken cancellationToken = default) =>
        StatText.PrepareAsync(stats.Concat(
            fragments.Values.SelectMany(v => v).SelectMany(v => v).SelectMany(v => v)), cancellationToken);

    public StatMatch[] MatchLine(ModLine line) => StatText.Match(StatText.StripCopySuffixes(line.Text),
        stats);
}
