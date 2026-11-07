using System.Text.Json;

namespace ExileSight.Core.Settings;

internal static class SettingsValidation
{
    public static void Validate(VersionedDocument document)
    {
        if (document is SettingsDocument s)
        {
            if (s.General is null || s.Overlay is null || s.Features is null || s.Hotkeys is null ||
                s.Features.Enabled is null || s.Features.Sections is null ||
                s.Features.Sections.Values.Any(x => x is null) || string.IsNullOrWhiteSpace(s.General.Language) ||
                s.Overlay.ScaleOverride is double scale && (!double.IsFinite(scale) || scale <= 0))
                throw new JsonException("Invalid settings sections.");
            if (s.Hotkeys.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) ||
                x.Key is < 1 or > 255 || ((int)x.Modifiers & ~15) != 0 ||
                string.IsNullOrWhiteSpace(x.Context) ||
                (x.Action is null) == string.IsNullOrWhiteSpace(x.FeatureCommand) ||
                x.Action is { } action && !Enum.IsDefined(action) ||
                x.Action == Platform.Abstractions.Input.ActionKind.SendChatCommand && string.IsNullOrWhiteSpace(x.Text)) ||
                s.Hotkeys.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != s.Hotkeys.Count)
                throw new JsonException("Invalid hotkey binding.");
        }
        if (document is ProfileCatalog c &&
            (c.Profiles is null || c.Profiles.Count == 0 ||
             c.Profiles.Any(x => x is null || !ValidId(x.Id) || string.IsNullOrWhiteSpace(x.Name)) ||
             c.Profiles.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != c.Profiles.Count ||
             !c.Profiles.Any(x => x.Id == c.ActiveProfileId)))
            throw new JsonException("Invalid profile catalog.");
    }

    internal static bool ValidId(string id) => id == "default" || Guid.TryParseExact(id, "N", out _);
}
