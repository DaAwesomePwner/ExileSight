// Frozen from 4ca85cc ItemInfoData.cs; only class/namespace names changed.
using ExileSight.Core.DataPacks;
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo.Tests.Reference;

/// <summary>App injects already loaded packs; this adapter performs no IO.</summary>
internal sealed class ReferenceItemInfoData(PackContent? bases, PackContent? mods, PackContent? stats) : IItemInfoData
{
    private readonly Dictionary<string, StatTranslation[][][]> fragments = (stats?.Translations ?? [])
        .ToDictionary(t => t.Id, t => t.English.Select(v => v.Text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
                new[] { t with { English = [v with { Text = line + "\n" }] } }).ToArray()).ToArray());

    public BaseEntry? FindBase(ParsedItem item) => bases?.Bases.FirstOrDefault(b =>
        ItemPackIdentity.ClassKey(b.ItemClass) == ItemPackIdentity.ClassKey(item.ItemClass) && b.Name == item.BaseType);

    // Exile-UI modules/item-checker.ahk:1436–1463 (MIT): name, category, type, all text and spawn tags; first match.
    public ModEntry? FindMod(ParsedItem item, IReadOnlyList<ModLine> group)
    {
        var basis = FindBase(item);
        if (basis is null || mods is null || stats?.Translations is null || group.Count == 0) return null;
        var first = group[0];
        if (first.AffixName is null || first.AffixType == AffixType.Unknown) return null;
        var matches = group.Select(MatchLine).ToArray();
        var tags = ItemPackIdentity.SpawnTags(item, basis);
        return mods.Mods.FirstOrDefault(m => m.Name == first.AffixName &&
            m.AffixType.Equals(first.AffixType.ToString(), StringComparison.OrdinalIgnoreCase) &&
            m.Domain == basis.Domain && (m.SpawnWeights.FirstOrDefault(w => tags.Contains(w.Tag))?.Weight ?? 0) > 0 &&
            m.TextTemplateIds.Length > 0 && matches.All(line => line.Any(match =>
                m.TextTemplateIds.Contains(match.TranslationId) && match.Values.All(v =>
                    m.Stats.Any(s => s.Id == v.StatId && v.Min <= s.Max && v.Max >= s.Min)))) &&
            m.TextTemplateIds.All(id => fragments.TryGetValue(id, out var variants) &&
                variants.Any(variant => variant.All(fragment => group.Any(line =>
                    StatText.Match(StatText.StripCopySuffixes(line.Text), fragment).Length > 0)))) &&
            m.Stats.All(s => matches.Any(line => line.Any(match => m.TextTemplateIds.Contains(match.TranslationId) &&
                match.Values.Any(v => v.StatId == s.Id)))));
    }

    public Task PrepareAsync(CancellationToken cancellationToken = default) =>
        StatText.PrepareAsync((stats?.Translations ?? []).Concat(
            fragments.Values.SelectMany(v => v).SelectMany(v => v).SelectMany(v => v)), cancellationToken);

    public StatMatch[] MatchLine(ModLine line) => StatText.Match(StatText.StripCopySuffixes(line.Text),
        stats?.Translations ?? []);
}
