using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Features;

public sealed record FeatureDefaultBinding(string Id, int Key, KeyModifiers Modifiers, bool Block,
    string Command, string Context = "gameFocused")
{
    public PersistedBinding ToSetting() => new()
    { Id = Id, Key = Key, Modifiers = Modifiers, Block = Block, Context = Context, FeatureCommand = Command };

    public static IEnumerable<PersistedBinding> Apply(IEnumerable<PersistedBinding> persisted,
        IEnumerable<FeatureDefaultBinding> defaults)
    {
        var entries = persisted.ToArray();
        var ids = entries.Where(b => b is not null).Select(b => b.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return entries.Concat(defaults.Where(b => ids.Add(b.Id)).Select(b => b.ToSetting()));
    }
}
