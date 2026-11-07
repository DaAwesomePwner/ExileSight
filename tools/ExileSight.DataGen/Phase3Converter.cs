using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;

namespace ExileSight.DataGen;

public static class Phase3Converter
{
    public const string GuidePath = "data/english/[leveltracker] default guide.json";
    public const string MapPath = "data/english/map-info.txt";
    public static string UpstreamUrl(string sha, string path)
    {
        RepoeConverter.ValidateSha(sha);
        return $"https://raw.githubusercontent.com/Lailloken/Exile-UI/{sha}/"
            + string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
    }
    public static byte[] NormalizeAuthored(byte[] bytes) => System.Text.Encoding.UTF8.GetBytes(
        System.Text.Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n"));
    public static byte[] Authored(string file)
    {
        using var stream = typeof(Phase3Converter).Assembly.GetManifestResourceStream("ExileSight.DataGen." + file)
            ?? throw new InvalidOperationException("Missing authored source.");
        using var buffer = new MemoryStream(); stream.CopyTo(buffer); return NormalizeAuthored(buffer.ToArray());
    }
    public static DataPack[] Convert(byte[] guide, byte[] areas, byte[] gems, byte[] map, string repoeSha, string exileSha,
        Func<string, byte[]>? readAuthored = null)
    {
        RepoeConverter.ValidateSha(repoeSha); RepoeConverter.ValidateSha(exileSha);
        byte[] Read(string file) => NormalizeAuthored((readAuthored ?? Authored)(file));
        var events = Read("events.json"); var leagues = Read("leagues.json");
        string Version(string kind) => kind switch
        {
            "guide" => PackCodec.Hash(System.Text.Encoding.UTF8.GetBytes(repoeSha + exileSha)),
            "mapmods" => exileSha,
            "events" => PackCodec.Hash(events),
            "leagues" => PackCodec.Hash(leagues),
            _ => throw new ArgumentException("Unknown pack kind.")
        };
        PackContent Content(string kind) => new("poe1/" + kind, Game.PoE1, PackCodec.SchemaVersion, Version(kind), [], []);
        var upstreamNotice = $"Exile-UI (https://github.com/Lailloken/Exile-UI), commit {exileSha}, MIT, Copyright (c) 2022 Lailloken.\n"
            + System.Text.Encoding.UTF8.GetString(Read("guide-notices.txt"));
        DataPack Pack(PackContent content, PackSource source, string attribution) => new(
            new(content.PackId, Game.PoE1, PackCodec.SchemaVersion, content.PackVersion, "unknown", DateTimeOffset.UnixEpoch,
                source, attribution, PackCodec.Hash(PackCodec.ContentBytes(content))), content);
        PackSource AuthoredSource(string file, byte[] bytes) => new("ExileSight authored data", PackCodec.Hash(bytes),
            new() { ["tools/ExileSight.DataGen/" + file] = PackCodec.Hash(bytes) });
        return [
            Pack(Content("guide") with { Guide = GuideConverter.Read(guide, areas, gems) },
                new("Lailloken/Exile-UI", exileSha, new()
                {
                    [UpstreamUrl(exileSha, GuidePath)] = PackCodec.Hash(guide),
                    [$"https://raw.githubusercontent.com/{RepoeConverter.Repository}/{repoeSha}/data/world_areas.json"] = PackCodec.Hash(areas),
                    [$"https://raw.githubusercontent.com/{RepoeConverter.Repository}/{repoeSha}/data/gems.json"] = PackCodec.Hash(gems)
                }), upstreamNotice + " Guide derived from HeartofPhos/exile-leveling (https://github.com/HeartofPhos/exile-leveling), MIT. " + RepoeConverter.Attribution(repoeSha)),
            Pack(Content("mapmods") with { MapMods = MapModsConverter.Read(map) },
                new("Lailloken/Exile-UI", exileSha, new() { [UpstreamUrl(exileSha, MapPath)] = PackCodec.Hash(map) }),
                upstreamNotice + " Game data © Grinding Gear Games; not affiliated with or endorsed by Grinding Gear Games."),
            Pack(Content("events") with { Events = new(JsonSerializer.Deserialize<EventDefinition[]>(events, PackJson.Default.Options)!) },
                AuthoredSource("events.json", events), "Event metadata authored by ExileSight. Matching dialogue remains in Core/Logs/EnglishPatterns.cs; game dialogue © Grinding Gear Games."),
            Pack(Content("leagues") with { Leagues = new(JsonSerializer.Deserialize<LeagueDefinition[]>(leagues, PackJson.Default.Options)!) },
                AuthoredSource("leagues.json", leagues), "Historical league facts curated by ExileSight; incomplete history, null end means unknown. Current leagues come from poe.ninja index-state at runtime.")
        ];
    }
}
