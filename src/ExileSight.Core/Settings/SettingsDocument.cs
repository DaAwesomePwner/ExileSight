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
    public Games.Game? Game { get; set; }
}

public sealed class SettingsDocument : VersionedDocument
{
    public PricesSettings Prices { get; set; } = new();
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
    public bool CheckForUpdates { get; set; } = true;
    public string UpdateChannel { get; set; } = "stable";
    public string Language { get; set; } = "en";
}

public sealed class PersistedBinding : ExtensibleSettings
{
    public string Id { get; set; } = "";
    private bool enabled = true;
    public bool Enabled
    {
        get => enabled;
        set { enabled = value; RuntimeDisabled = false; }
    }
    // In-memory provenance distinguishes an automatic disable from an explicit user edit.
    internal bool RuntimeDisabled { get; private set; }
    internal void DisableForRuntime() { enabled = false; RuntimeDisabled = true; }
    public int Key { get; set; }
    public KeyModifiers Modifiers { get; set; }
    public bool Block { get; set; } = true;
    public string Context { get; set; } = "gameFocused";
    public ActionKind? Action { get; set; }
    public string? Text { get; set; }
    public bool Advanced { get; set; }
    public string? FeatureCommand { get; set; }
}

public enum CardPlacementMode { BesideCursor, Docked }
public enum OverlayDensity { Comfortable, Compact }
public enum OverlayContrast { Standard, HighContrast }

public sealed class OverlaySettings : ExtensibleSettings
{
    public bool Enabled { get; set; } = true;
    // Keep legacy positive stored overrides valid; UI edits use SetUiScaleOverride.
    public double? ScaleOverride { get; set; }
    public CardPlacementMode PlacementMode { get; set; } = CardPlacementMode.BesideCursor;
    /// <summary>Card top-left as fractions of the full client, then clamped to visible space.</summary>
    public double DockedX { get; set; } = .5;
    public double DockedY { get; set; } = .5;
    public OverlayDensity Density { get; set; } = OverlayDensity.Comfortable;
    public OverlayContrast Contrast { get; set; } = OverlayContrast.Standard;

    /// <summary>Auto is null; newly chosen fixed UI scales must be in 0.8–1.5.</summary>
    public void SetUiScaleOverride(double? value)
    {
        if (value is double scale && (!double.IsFinite(scale) || scale < .8 || scale > 1.5))
            throw new ArgumentOutOfRangeException(nameof(value));
        ScaleOverride = value;
    }
}

public sealed class FeatureSettings : ExtensibleSettings
{
    public Dictionary<string, bool> Enabled { get; set; } = [];
    public Dictionary<string, Dictionary<string, JsonElement>> Sections { get; set; } = [];
}
