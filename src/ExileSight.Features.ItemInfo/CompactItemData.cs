using ExileSight.Core.DataPacks;

namespace ExileSight.Features.ItemInfo;

/// <summary>Detach wire DTOs and share repeated immutable values within one adapter/version.
/// Pools are construction-local: they do not keep old packs or arbitrary strings alive.</summary>
internal sealed class CompactItemData
{
    private readonly Dictionary<string, string> strings = new(StringComparer.Ordinal);
    private readonly Pool<string> stringArrays = new();
    private readonly Pool<SpawnWeight> weightArrays = new();
    private readonly Pool<ModStat> statArrays = new();
    private readonly Dictionary<SpawnWeight, SpawnWeight> weights = [];
    private readonly Dictionary<ModStat, ModStat> stats = [];
    private readonly Dictionary<StatCondition, StatCondition> conditions = [];
    private string Text(string value)
    {
        if (strings.TryGetValue(value, out var shared)) return shared;
        strings.Add(value, value); return value;
    }
    private string[] Texts(string[] values) => stringArrays.Share(values.Select(Text).ToArray());
    private static T Share<T>(Dictionary<T, T> pool, T value) where T : notnull
    {
        if (pool.TryGetValue(value, out var shared)) return shared;
        pool.Add(value, value); return value;
    }
    internal BaseEntry[] Bases(BaseEntry[] entries) => entries.Select(b => b with
    {
        Id = Text(b.Id), Name = Text(b.Name), ItemClass = Text(b.ItemClass), Domain = Text(b.Domain),
        Tags = Texts(b.Tags), ImplicitIds = Texts(b.ImplicitIds),
        Requirements = b.Requirements.ToDictionary(p => Text(p.Key), p => p.Value),
        Defences = b.Defences.ToDictionary(p => Text(p.Key), p => p.Value)
    }).ToArray();
    internal ModEntry[] Mods(ModEntry[] entries) => entries.Select(m => m with
    {
        Id = Text(m.Id), Name = Text(m.Name), AffixType = Text(m.AffixType), Domain = Text(m.Domain),
        GenerationType = Text(m.GenerationType), Family = Text(m.Family), Groups = Texts(m.Groups),
        TextTemplateIds = Texts(m.TextTemplateIds),
        Stats = statArrays.Share(m.Stats.Select(s => Share(stats, s with { Id = Text(s.Id) })).ToArray()),
        SpawnWeights = weightArrays.Share(m.SpawnWeights.Select(w => Share(weights, w with { Tag = Text(w.Tag) })).ToArray())
    }).ToArray();
    internal StatTranslation[] Translations(StatTranslation[] entries) => entries.Select(t => t with
    {
        Id = Text(t.Id), StatIds = Texts(t.StatIds), English = t.English.Select(v => v with
        {
            Text = Text(v.Text), Formats = Texts(v.Formats),
            Conditions = v.Conditions.Select(c => Share(conditions, c)).ToArray(),
            IndexHandlers = v.IndexHandlers.Select(Texts).ToArray()
        }).ToArray()
    }).ToArray();

    private sealed class Pool<T> : IEqualityComparer<T[]>
    {
        private readonly Dictionary<T[], T[]> values;
        internal Pool() => values = new(this);
        internal T[] Share(T[] array) => CompactItemData.Share(values, array);
        public bool Equals(T[]? a, T[]? b) => ReferenceEquals(a, b) || a is not null && b is not null && a.SequenceEqual(b);
        public int GetHashCode(T[] value)
        {
            var hash = new HashCode();
            foreach (var item in value) hash.Add(item);
            return hash.ToHashCode();
        }
    }
}
