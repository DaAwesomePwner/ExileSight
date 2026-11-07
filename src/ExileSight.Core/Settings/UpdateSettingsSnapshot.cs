using System.Text.Json;
using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.Core.Settings;

/// <summary>Reads only updater preferences. Never opens a writable store, migrates, recovers,
/// quarantines, creates directories, or cleans temporary files. Malformed files propagate to
/// the updater's Unavailable status rather than enabling updates with recovered defaults.</summary>
public sealed record UpdateSettingsSnapshot(GeneralSettings Settings, IReadOnlyList<string> Warnings)
{
    public static UpdateSettingsSnapshot Read(IAppPaths paths)
    {
        var files = new RootedFiles(paths.RootDirectory);
        var directory = paths.GetSettingsDirectory("poe1");
        var catalogPath = files.Check(Path.Combine(directory, "profiles.json"));
        var id = "default";
        if (File.Exists(catalogPath))
        {
            using var catalog = JsonDocument.Parse(File.ReadAllBytes(catalogPath));
            if (catalog.RootElement.TryGetProperty("activeProfileId", out var active))
                id = active.GetString() ?? throw new JsonException("Null active profile.");
        }
        if (!SettingsValidation.ValidId(id)) throw new JsonException("Invalid active profile.");
        var path = files.Check(Path.Combine(directory, id + ".json"));
        var settings = new GeneralSettings();
        var warnings = new List<string>();
        if (File.Exists(path))
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            if (document.RootElement.TryGetProperty("general", out var general))
            {
                if (general.TryGetProperty("checkForUpdates", out var enabled))
                    settings.CheckForUpdates = enabled.GetBoolean();
                if (general.TryGetProperty("updateChannel", out var channel))
                    settings.UpdateChannel = channel.ValueKind == JsonValueKind.String ? channel.GetString()! : null!;
            }
        }
        SettingsValidation.NormalizeUpdateChannel(settings, warnings);
        return new(settings, warnings);
    }
}
