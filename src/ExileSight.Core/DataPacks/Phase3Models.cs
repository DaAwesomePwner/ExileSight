namespace ExileSight.Core.DataPacks;

public sealed record GuideCondition(string Kind, string[] Values);
public sealed record GuideLine(string Text, string[] AreaIds, string[] QuestIds, string[] LevelHints);
public sealed record GuidePage(int Act, int Page, GuideCondition? Condition, GuideLine[] Lines);
public sealed record GuideArea(string Id, string Name, int Act, int Level, bool IsTown, bool HasWaypoint, string[] Connections);
public sealed record GemLevel(int Level, int? RequiredLevel, Dictionary<string, int> Requirements);
public sealed record GemReward(int Act, string QuestId, string Quest, string[] Classes);
public sealed record GuideGem(string Id, string Name, GemLevel[] Levels, GemReward? QuestReward);
public sealed record GuideContent(GuidePage[] Pages, GuideArea[] Areas, GuideGem[] Gems);
// Each pattern is an alternative; its pipe-separated lines must match together. '#' is numeric.
public sealed record MapModEntry(string Id, string Type, string Text, string[] Patterns);
public sealed record MapModsContent(MapModEntry[] Entries);
public sealed record EventDefinition(string Id, string DisplayName, bool EnabledByDefault, string Category);
public sealed record EventsContent(EventDefinition[] Entries);
public sealed record LeagueDefinition(string Name, DateOnly Start, DateOnly? End);
public sealed record LeaguesContent(LeagueDefinition[] Entries);
