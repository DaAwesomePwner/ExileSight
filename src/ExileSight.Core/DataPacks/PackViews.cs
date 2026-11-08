using System.Text.Json;

namespace ExileSight.Core.DataPacks;

// Immutable read models; wire models remain unchanged for generation and validation.
public sealed record GuideConditionData(string Kind, IReadOnlyList<string> Values);
public sealed record GuideLineData(string Text, IReadOnlyList<string> AreaIds, IReadOnlyList<string> QuestIds, IReadOnlyList<string> LevelHints);
public sealed record GuidePageData(int Act, int Page, GuideConditionData? Condition, IReadOnlyList<GuideLineData> Lines);
public sealed record GuideAreaData(string Id, string Name, int Act, int Level, bool IsTown, bool HasWaypoint, IReadOnlyList<string> Connections);
public sealed record GemLevelData(int Level, int? RequiredLevel, IReadOnlyDictionary<string, int> Requirements);
public sealed record GemRewardData(int Act, string QuestId, string Quest, IReadOnlyList<string> Classes);
public sealed record GuideGemData(string Id, string Name, IReadOnlyList<GemLevelData> Levels, GemRewardData? QuestReward);
public sealed record GuideData(IReadOnlyList<GuidePageData> Pages, IReadOnlyList<GuideAreaData> Areas, IReadOnlyList<GuideGemData> Gems);
public sealed record MapModData(string Id, string Type, string Text, IReadOnlyList<string> Patterns);
public sealed record MapModsData(IReadOnlyList<MapModData> Entries);
public sealed record EventsData(IReadOnlyList<EventDefinition> Entries);
public sealed record LeaguesData(IReadOnlyList<LeagueDefinition> Entries);
/// <summary>Immutable document view for the original bases/mods/stats packs.</summary>
public sealed record PackDocument(JsonElement Content);

internal static class PackViews
{
    internal static Type? TypeFor(string id) => id switch
    {
        "poe1/guide" => typeof(GuideData), "poe1/mapmods" => typeof(MapModsData),
        "poe1/events" => typeof(EventsData), "poe1/leagues" => typeof(LeaguesData),
        "poe1/bases" or "poe1/mods" or "poe1/stats" => typeof(PackDocument), _ => null
    };
    private static IReadOnlyList<T> List<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
    internal static object Parse(Stream bytes, string id)
    {
        if (TypeFor(id) == typeof(PackDocument))
        {
            using var document = JsonDocument.Parse(bytes);
            return new PackDocument(document.RootElement.Clone());
        }
        var c = JsonSerializer.Deserialize(bytes, PackJson.Default.PackContent)!;
        return id switch
        {
            "poe1/guide" => GuideViewBuilder.Build(c.Guide!),
            "poe1/mapmods" => new MapModsData(List(c.MapMods!.Entries.Select(e => new MapModData(e.Id, e.Type, e.Text, List(e.Patterns))))),
            "poe1/events" => new EventsData(List(c.Events!.Entries)),
            "poe1/leagues" => new LeaguesData(List(c.Leagues!.Entries)),
            _ => throw new ArgumentException("Unknown pack ID")
        };
    }
}
