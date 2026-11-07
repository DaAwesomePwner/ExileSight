using System.Text.Json;

namespace ExileSight.Core.Settings;

internal static class SettingsValidation
{
    public static void Validate(VersionedDocument document, Games.Game? expectedGame = null)
    {
        if (expectedGame is not null && document.Game != expectedGame)
            throw new JsonException("Missing, null, or mismatched game for the requested settings.");
        if (document.Game is { } game && !Enum.IsDefined(game)) throw new JsonException("Invalid game value.");
        if (document is SettingsDocument s)
        {
            if (s.General is null || s.Overlay is null || s.Features is null || s.Hotkeys is null ||
                s.Features.Enabled is null || s.Features.Sections is null ||
                s.Features.Sections.Values.Any(x => x is null) || string.IsNullOrWhiteSpace(s.General.Language) ||
                s.General.UpdateChannel is not ("stable" or "prerelease") ||
                s.Overlay.ScaleOverride is double scale && (!double.IsFinite(scale) || scale <= 0))
                throw new JsonException("Invalid settings sections.");
            if (s.Hotkeys.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) ||
                x.Key is < 1 or > 255 || ((int)x.Modifiers & ~15) != 0 ||
                string.IsNullOrWhiteSpace(x.Context) ||
                (x.Action is null) == string.IsNullOrWhiteSpace(x.FeatureCommand) ||
                x.Action == Platform.Abstractions.Input.ActionKind.SendChatCommand && string.IsNullOrWhiteSpace(x.Text)) ||
                s.Hotkeys.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != s.Hotkeys.Count)
                throw new JsonException("Invalid hotkey binding.");
        }
        if (document is ProfileCatalog c &&
            (c.Profiles is null || c.Profiles.Count == 0 ||
             c.Profiles.Any(x => x is null || !ValidId(x.Id) || string.IsNullOrWhiteSpace(x.Name)) ||
             c.Profiles.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != c.Profiles.Count ||
             !c.Profiles.Any(x => string.Equals(x.Id, c.ActiveProfileId, StringComparison.OrdinalIgnoreCase))))
            throw new JsonException("Invalid profile catalog.");
    }

    public static void DisableUnknownBindings(VersionedDocument document, List<string> warnings)
    {
        if (document is not SettingsDocument settings) return;
        foreach (var binding in settings.Hotkeys.Where(x => x.Action is { } action && !Enum.IsDefined(action)))
        {
            binding.Enabled = false;
            var warning = $"Binding '{binding.Id}' disabled: unknown action kind {binding.Action}.";
            if (!warnings.Contains(warning)) warnings.Add(warning);
        }
    }

    /// <summary>Unknown bindings cannot be edited safely by this version. Preserve their stored
    /// enabled flag and action kind by ID even when a runtime snapshot is written back wholesale.
    /// Remove the old ID to replace such a binding; unrelated binding edits remain available.</summary>
    public static void PreserveUnknownBindings(VersionedDocument stored, VersionedDocument candidate)
    {
        if (stored is not SettingsDocument original || candidate is not SettingsDocument updated ||
            updated.Hotkeys is null) return;
        var unknown = original.Hotkeys.Where(x => x.Action is { } action && !Enum.IsDefined(action))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var binding in updated.Hotkeys)
        {
            if (binding is null || binding.Id is null || !unknown.TryGetValue(binding.Id, out var previous)) continue;
            binding.Enabled = previous.Enabled;
            binding.Action = previous.Action;
        }
    }

    internal static bool ValidId(string id) => string.Equals(id, "default", StringComparison.OrdinalIgnoreCase) || Guid.TryParseExact(id, "N", out _);
}
