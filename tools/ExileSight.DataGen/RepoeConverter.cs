// Consumes the RePoE-fork schema: https://github.com/repoe-fork/repoe/tree/master/RePoE/docs
// No upstream game data is embedded in this converter.
using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;

namespace ExileSight.DataGen;

public static class RepoeConverter
{
    public const string Repository = "repoe-fork/repoe-fork.github.io";
    public static string Attribution(string sha) =>
        $"Data from RePoE fork at {sha.ToLowerInvariant()} (https://github.com/{Repository}); RePoE code: MIT. Game data © Grinding Gear Games; published with the owner's approval per D-047.";
    public static void ValidateSha(string sha)
    {
        if (sha.Length != 40 || !sha.All(Uri.IsHexDigit)) throw new ArgumentException("A full 40-character commit SHA is required.");
    }

    public static DataPack[] Convert(byte[] basesJson, byte[] modsJson, string sha)
    {
        ValidateSha(sha);
        using var basesDoc = JsonDocument.Parse(basesJson);
        using var modsDoc = JsonDocument.Parse(modsJson);
        var bases = basesDoc.RootElement.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => ReadBase(p.Name, p.Value)).ToArray();
        var mods = modsDoc.RootElement.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => ReadMod(p.Name, p.Value)).ToArray();
        var tiers = TierDerivation.Derive(bases, mods);
        var source = new PackSource(Repository, sha.ToLowerInvariant(), new()
        {
            ["data/base_items.json"] = PackCodec.Hash(basesJson), ["data/mods.json"] = PackCodec.Hash(modsJson)
        });
        return new[] { "bases", "mods" }.Select(kind =>
        {
            var content = new PackContent("poe1/" + kind, Game.PoE1, PackCodec.SchemaVersion, sha.ToLowerInvariant(),
                kind == "bases" ? bases : [], kind == "mods" ? mods : [],
                kind == "mods" ? tiers.Ladders : [], kind == "mods" ? tiers.Contexts : []);
            // Unknown source patch/time are explicit, never fabricated from the local clock.
            var manifest = new PackManifest(content.PackId, Game.PoE1, PackCodec.SchemaVersion, content.PackVersion,
                "unknown", DateTimeOffset.UnixEpoch, source, Attribution(sha), PackCodec.Hash(PackCodec.ContentBytes(content)));
            return new DataPack(manifest, content);
        }).ToArray();
    }

    private static BaseEntry ReadBase(string id, JsonElement value)
    {
        var requirements = new Dictionary<string, int>();
        if (value.TryGetProperty("requirements", out var req) && req.ValueKind == JsonValueKind.Object)
            foreach (var p in req.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) requirements[p.Name] = p.Value.GetInt32();
        var defences = new Dictionary<string, NumericRange>();
        if (value.TryGetProperty("properties", out var props))
            foreach (var name in new[] { "armour", "energy_shield", "evasion" })
                if (props.TryGetProperty(name, out var range))
                    defences[name] = range.ValueKind == JsonValueKind.Number ? new(range.GetInt32(), range.GetInt32()) :
                        new(range.GetProperty("min").GetInt32(), range.GetProperty("max").GetInt32());
        return new(id, Text(value, "name"), Text(value, "item_class"), Strings(value, "tags"),
            value.GetProperty("drop_level").GetInt32(), requirements, Strings(value, "implicits"), defences, Domain: OptionalText(value, "domain", "item"));
    }

    private static ModEntry ReadMod(string id, JsonElement value)
    {
        var generation = Text(value, "generation_type");
        var stats = value.GetProperty("stats").EnumerateArray().Select(s =>
            new ModStat(Text(s, "id"), s.GetProperty("min").GetInt32(), s.GetProperty("max").GetInt32())).ToArray();
        var weights = value.GetProperty("spawn_weights").EnumerateArray().Select(w =>
            new SpawnWeight(Text(w, "tag"), w.GetProperty("weight").GetInt32())).ToArray();
        return new(id, Text(value, "name"), generation, Text(value, "domain"), generation,
            value.GetProperty("required_level").GetInt32(), Strings(value, "groups"), stats, weights,
            stats.Select(s => s.Id).ToArray(), OptionalText(value, "type", ""));
    }

    private static string OptionalText(JsonElement value, string name, string fallback) =>
        value.TryGetProperty(name, out var field) ? field.GetString() ?? fallback : fallback;
    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? "";
    private static string[] Strings(JsonElement value, string name) => value.GetProperty(name).EnumerateArray()
        .Select(s => s.GetString() ?? "").ToArray();
}
