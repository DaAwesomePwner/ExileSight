using System.IO;
using ExileSight.Core.Diagnostics;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.App.Tray;

public sealed class SettingsDiagnostics(ProfileStore profiles,
    ITransactionalSettingsStore<SettingsDocument> settings, IAppPaths paths) : IDebugSection
{
    public string Title => "Settings";
    public bool OverlayAttached { get; set; }
    public bool InputAttached { get; set; }
    public IReadOnlyList<KeyValuePair<string, string>> Snapshot() =>
    [
        new("Source", settings is JsonSettingsStore<SettingsDocument> { LoadedDefaults: true }
            ? "defaults" : profiles.ActiveProfileId + " — " +
                Path.Combine(paths.GetSettingsDirectory("poe1"), profiles.ActiveProfileId + ".json")),
        new("Load/save warnings", string.Join("\n", profiles.Warnings.Concat(settings.Warnings))),
        new("Overlay consumer attached", OverlayAttached.ToString()),
        new("Input consumer attached", InputAttached.ToString())
    ];
}
