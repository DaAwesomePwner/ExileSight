using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExileSight.Core.Settings;

public interface IMigration
{
    int FromVersion { get; }
    void Apply(JsonObject document);
}

/// <summary>Missing/invalid versions are corrupt. Version 1 is the initial schema; no implicit v0 exists.</summary>
public sealed class MigrationPipeline
{
    private readonly Dictionary<int, IMigration> migrations;
    public int CurrentVersion { get; }

    public MigrationPipeline(int currentVersion = 1, IEnumerable<IMigration>? migrations = null)
    {
        if (currentVersion < 1) throw new ArgumentOutOfRangeException(nameof(currentVersion));
        CurrentVersion = currentVersion;
        this.migrations = (migrations ?? []).ToDictionary(x => x.FromVersion);
        for (var version = 1; version < currentVersion; version++)
            if (!this.migrations.ContainsKey(version))
                throw new ArgumentException($"Missing migration from {version}.", nameof(migrations));
    }

    internal static int Version(JsonObject document)
    {
        if (document["schemaVersion"] is not JsonValue value ||
            !value.TryGetValue<int>(out var version) || version < 1)
            throw new JsonException("Missing or invalid schemaVersion.");
        return version;
    }

    public void Apply(JsonObject document)
    {
        var version = Version(document);
        if (version > CurrentVersion) throw new InvalidOperationException("Newer settings schema.");
        while (version < CurrentVersion)
        {
            // Migration failures propagate without modifying the original file.
            migrations[version].Apply(document);
            document["schemaVersion"] = ++version;
        }
    }
}
