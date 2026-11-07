using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace ExileSight.Core.Settings;

internal sealed record SettingsLoad<T>(T Value, bool ReadOnly, bool Dirty, bool RotateBackup);

internal sealed class SettingsReader<T>(RootedFiles files, string path, JsonTypeInfo<T> typeInfo,
    MigrationPipeline migrations, List<string> warnings, Games.Game? expectedGame = null) where T : VersionedDocument
{
    public SettingsLoad<T> Load(Func<T> defaults)
    {
        var failed = false;
        foreach (var source in new[] { path, path + ".bak" })
        {
            if (!File.Exists(files.Check(source))) continue;
            try
            {
                var result = Read(source, defaults);
                if (source != path)
                {
                    warnings.Add("Recovered settings from the rolling backup.");
                    return result with { Dirty = !result.ReadOnly };
                }
                return result;
            }
            catch (JsonException e)
            {
                failed = true;
                warnings.Add("Invalid settings in " + Path.GetFileName(source) + ": " + e.Message);
                if (source == path) files.Quarantine(path);
            }
        }
        return new(Fresh(defaults), false, failed, !failed);
    }

    private SettingsLoad<T> Read(string source, Func<T> defaults)
    {
        var bytes = File.ReadAllBytes(files.Check(source));
        using var json = JsonDocument.Parse(bytes);
        RejectDuplicates(json.RootElement);
        var node = JsonNode.Parse(bytes) as JsonObject ?? throw new JsonException("Settings must be a JSON object.");
        var version = MigrationPipeline.Version(node);
        if (version > migrations.CurrentVersion)
        {
            warnings.Add($"Schema {version} is newer than supported schema {migrations.CurrentVersion}; settings are read-only.");
            try { return new(Deserialize(node), true, false, false); }
            catch (JsonException e)
            {
                warnings.Add("Newer settings cannot be displayed; showing defaults: " + e.Message);
                return new(Fresh(defaults), true, false, false);
            }
        }
        if (expectedGame is not null &&
            (node["game"] is not JsonValue gameValue || !gameValue.TryGetValue<int>(out var game) || game != (int)expectedGame))
            throw new JsonException("Missing, null, or mismatched game for the requested settings.");
        if (version < migrations.CurrentVersion)
        {
            // Only data-validation failures recover. Programming errors propagate with original files untouched.
            T migrated;
            try
            {
                migrations.Apply(node);
                migrated = Deserialize(node);
            }
            catch (MigrationValidationException e) { throw new JsonException("Settings migration failed: " + e.Message, e); }
            files.PreserveVersion(source, path, version);
            return new(migrated, false, true, true);
        }
        return new(Deserialize(node), false, false, true);
    }

    private T Deserialize(JsonObject node)
    {
        var loaded = JsonSerializer.Deserialize(node, typeInfo) ?? throw new JsonException("Null settings.");
        SettingsValidation.Validate(loaded, expectedGame, warnings);
        return loaded;
    }

    private T Fresh(Func<T> defaults)
    {
        var fresh = defaults();
        fresh.SchemaVersion = migrations.CurrentVersion;
        SettingsValidation.Validate(fresh, expectedGame, warnings);
        return JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(fresh, typeInfo), typeInfo)!;
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate JSON property: " + property.Name);
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicates(item);
    }
}
