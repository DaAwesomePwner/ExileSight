using System.Text.Json;
using System.Text.Json.Serialization;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Settings;

/// <summary>Unknown fields are preserved at every settings section for forward-compatible round trips.</summary>
public class ExtensibleSettings
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
}

public class VersionedDocument : ExtensibleSettings
{
    public int SchemaVersion { get; set; } = 1;
}

public sealed class SettingsDocument : VersionedDocument
{
    public GeneralSettings General { get; set; } = new();
    public List<PersistedBinding> Hotkeys { get; set; } =
    [
        new() { Id = "copy-item", Key = 0x76, Action = ActionKind.CopyItem },
        new() { Id = "hideout", Key = 0x77, Action = ActionKind.SendChatCommand, Text = "/hideout" }
    ];
    public OverlaySettings Overlay { get; set; } = new();
    public FeatureSettings Features { get; set; } = new();
}

public sealed class GeneralSettings : ExtensibleSettings
{
    public string? ClientTxtPath { get; set; }
    public bool FirstRunCompleted { get; set; }
    public string Language { get; set; } = "en";
}

public sealed class PersistedBinding : ExtensibleSettings
{
    public string Id { get; set; } = "";
    public int Key { get; set; }
    public KeyModifiers Modifiers { get; set; }
    public bool Block { get; set; } = true;
    public string Context { get; set; } = "gameFocused";
    public ActionKind? Action { get; set; }
    public string? Text { get; set; }
    public bool Advanced { get; set; }
    public string? FeatureCommand { get; set; }
}

public sealed class OverlaySettings : ExtensibleSettings
{
    public bool Enabled { get; set; } = true;
    public double? ScaleOverride { get; set; }
}

public sealed class FeatureSettings : ExtensibleSettings
{
    public Dictionary<string, bool> Enabled { get; set; } = [];
    public Dictionary<string, Dictionary<string, JsonElement>> Sections { get; set; } = [];
}
