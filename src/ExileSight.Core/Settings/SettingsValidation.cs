using System.Text.Json;

namespace ExileSight.Core.Settings;

internal static class SettingsValidation
{
    public static void Validate(VersionedDocument document, Games.Game? expectedGame = null, List<string>? warnings = null)
    {
        if (expectedGame is not null && document.Game != expectedGame)
            throw new JsonException("Missing, null, or mismatched game for the requested settings.");
        if (document.Game is { } game && !Enum.IsDefined(game)) throw new JsonException("Invalid game value.");
        if (document is SettingsDocument s)
        {
            s.Prices ??= new();
            if (s.General is null || s.Overlay is null || s.Features is null || s.Hotkeys is null ||
                s.Features.Enabled is null || s.Features.Sections is null ||
                s.Features.Sections.Values.Any(x => x is null) || string.IsNullOrWhiteSpace(s.General.Language) ||
                s.Overlay.ScaleOverride is double scale && (!double.IsFinite(scale) || scale <= 0))
                throw new JsonException("Invalid settings sections.");
            if (!Enum.IsDefined(s.Overlay.PlacementMode) || !Enum.IsDefined(s.Overlay.Density) ||
                !Enum.IsDefined(s.Overlay.Contrast) || !ValidFraction(s.Overlay.DockedX) ||
                !ValidFraction(s.Overlay.DockedY))
                throw new JsonException("Invalid overlay preferences.");
            NormalizeUpdateChannel(s.General, warnings);
            if (s.Hotkeys.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id)) ||
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
        foreach (var binding in settings.Hotkeys)
        {
            var reasons = BindingProblems(binding).ToArray();
            if (reasons.Length == 0) continue;
            binding.DisableForRuntime();
            foreach (var reason in reasons)
            {
                var warning = $"Binding '{binding.Id}' disabled: {reason}.";
                if (!warnings.Contains(warning)) warnings.Add(warning);
            }
        }
    }

    private static IEnumerable<string> BindingProblems(PersistedBinding binding)
    {
        if (binding.Key is < 1 or > 255) yield return "key must be in 1..255";
        if (((int)binding.Modifiers & ~15) != 0) yield return "unknown modifier bits";
        if (string.IsNullOrWhiteSpace(binding.Context)) yield return "blank context";
        if ((binding.Action is null) == string.IsNullOrWhiteSpace(binding.FeatureCommand))
            yield return "exactly one Action or FeatureCommand is required";
        if (binding.Action == Platform.Abstractions.Input.ActionKind.SendChatCommand && string.IsNullOrWhiteSpace(binding.Text))
            yield return "SendChatCommand requires text";
        if (binding.Action is { } action && !Enum.IsDefined(action)) yield return $"unknown action kind {action}";
    }

    /// <summary>Preserve unresolved bindings when a runtime snapshot is written back wholesale.
    /// Once a binding is repaired, honor all of the caller's values, including Enabled.</summary>
    public static void PreserveUnknownBindings(VersionedDocument stored, VersionedDocument candidate)
    {
        if (stored is not SettingsDocument original || candidate is not SettingsDocument updated ||
            updated.Hotkeys is null) return;
        var unknown = original.Hotkeys.Where(x => BindingProblems(x).Any())
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var binding in updated.Hotkeys)
        {
            if (binding is null || binding.Id is null || !unknown.TryGetValue(binding.Id, out var previous)) continue;
            var repaired = binding.Key != previous.Key || binding.Modifiers != previous.Modifiers ||
                binding.Context != previous.Context || binding.Action != previous.Action ||
                binding.FeatureCommand != previous.FeatureCommand || binding.Text != previous.Text;
            // Rebuilt/JSON-cloned candidates carry their own Enabled value. RuntimeDisabled
            // only preserves an untouched flag on this store's original runtime snapshot.
            if (repaired && !binding.RuntimeDisabled) continue;
            binding.Enabled = previous.Enabled;
            if (previous.Action is { } action && !Enum.IsDefined(action)) binding.Action = previous.Action;
        }
    }

    internal static void NormalizeUpdateChannel(GeneralSettings settings, List<string>? warnings)
    {
        var channel = settings.UpdateChannel?.ToLowerInvariant();
        if (channel is not ("stable" or "prerelease"))
        {
            const string warning = "Unknown update channel; using stable.";
            if (warnings is not null && !warnings.Contains(warning)) warnings.Add(warning);
            channel = "stable";
        }
        settings.UpdateChannel = channel;
    }

    private static bool ValidFraction(double value) => double.IsFinite(value) && value >= 0 && value <= 1;

    internal static bool ValidId(string id) => string.Equals(id, "default", StringComparison.OrdinalIgnoreCase) || Guid.TryParseExact(id, "N", out _);
}
