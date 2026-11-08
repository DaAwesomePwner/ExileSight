using System.Collections.ObjectModel;

namespace ExileSight.Core.DataPacks;

internal static class GuideViewBuilder
{
    private static IReadOnlyList<T> List<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
    internal static GuideData Build(GuideContent c)
    {
        // Most of the ~35k gem levels repeat the same small requirement maps.
        var requirements = new Dictionary<Dictionary<string, int>, IReadOnlyDictionary<string, int>>(new RequirementsComparer());
        var levels = new Dictionary<GemLevelData, GemLevelData>();
        GemLevelData Level(GemLevel value)
        {
            if (!requirements.TryGetValue(value.Requirements, out var shared))
            {
                shared = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(value.Requirements));
                requirements.Add(value.Requirements, shared);
            }
            var candidate = new GemLevelData(value.Level, value.RequiredLevel, shared);
            if (levels.TryGetValue(candidate, out var level)) return level;
            levels.Add(candidate, candidate); return candidate;
        }
        return new(
            List(c.Pages.Select(p => new GuidePageData(p.Act, p.Page,
                p.Condition is { } condition ? new(condition.Kind, List(condition.Values)) : null,
                List(p.Lines.Select(l => new GuideLineData(l.Text, List(l.AreaIds), List(l.QuestIds), List(l.LevelHints))))))),
            List(c.Areas.Select(a => new GuideAreaData(a.Id, a.Name, a.Act, a.Level, a.IsTown, a.HasWaypoint, List(a.Connections)))),
            List(c.Gems.Select(g => new GuideGemData(g.Id, g.Name, List(g.Levels.Select(Level)),
                g.QuestReward is { } r ? new(r.Act, r.QuestId, r.Quest, List(r.Classes)) : null))));
    }
    private sealed class RequirementsComparer : IEqualityComparer<Dictionary<string, int>>
    {
        public bool Equals(Dictionary<string, int>? a, Dictionary<string, int>? b) =>
            ReferenceEquals(a, b) || a is not null && b is not null && a.Count == b.Count &&
            a.All(p => b.TryGetValue(p.Key, out var value) && value == p.Value);
        public int GetHashCode(Dictionary<string, int> value)
        {
            var hash = 0;
            foreach (var p in value) hash ^= HashCode.Combine(StringComparer.Ordinal.GetHashCode(p.Key), p.Value);
            return hash;
        }
    }
}
