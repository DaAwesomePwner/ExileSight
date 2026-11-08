using ExileSight.Core.Features;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Input;

public sealed record BindingMapping(IReadOnlyList<HotkeyBinding> Bindings, IReadOnlyList<string> Rejected);
public static class BindingMapper
{
    // Conservative PoE chat cap, measured in UTF-16 code units before input synthesis.
    public const int MaximumChatLength = 400;
    public static BindingMapping Map(IEnumerable<PersistedBinding?> source, HotkeyConfiguration contexts,
        IEnumerable<IFeatureCommandHandler> handlers)
    {
        var registered = handlers.ToArray();
        var commands = registered.Select(h => h.Command).ToHashSet(StringComparer.Ordinal);
        var overlayOnly = registered.Where(h => h is IFeatureCommandPolicy { Kind: CommandKind.OverlayOnly })
            .Select(h => h.Command).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bindings = new List<HotkeyBinding>();
        var rejected = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var chords = new HashSet<(string, int, KeyModifiers)>();
        foreach (var b in source)
        {
            if (b is null) { rejected.Add("<null>: Binding required"); continue; }
            var error = b.FeatureCommand is { } command && overlayOnly.Contains(command)
                ? "Overlay-only command requires a physical overlay click"
                : Validate(b, contexts, commands);
            if (!b.Enabled) error = error is null ? "Disabled in settings" : $"Disabled in settings: {error}";
            if (error is null && ids.Contains(b.Id)) error = "Duplicate ID";
            if (error is null && chords.Contains((b.Context, b.Key, b.Modifiers)))
                error = "Duplicate key and modifiers in context";
            if (error is not null) { rejected.Add($"{b.Id}: {error}"); continue; }
            ids.Add(b.Id);
            chords.Add((b.Context, b.Key, b.Modifiers));
            GameAction? action = b.Action switch
            {
                ActionKind.CopyItem => new GameAction.CopyItem(b.Advanced),
                ActionKind.SendChatCommand => new GameAction.SendChatCommand(b.Text!),
                ActionKind.PasteSearch => new GameAction.PasteSearch(b.Text!),
                ActionKind.SetPriceNote => new GameAction.SetPriceNote(b.Text!),
                _ => null
            };
            var handler = registered.FirstOrDefault(h => h.Command == b.FeatureCommand);
            bindings.Add(new(b.Id, b.Key, b.Modifiers, b.Block, action, b.Context, b.FeatureCommand,
                handler?.FeatureId ?? "builtin", handler?.RequiredAction, handler is IPressCommandHandler));
        }
        return new(bindings.AsReadOnly(), rejected.AsReadOnly());
    }
    private static string? Validate(PersistedBinding b, HotkeyConfiguration contexts, HashSet<string> commands)
    {
        if (string.IsNullOrWhiteSpace(b.Id)) return "ID required";
        if (b.Key is < 1 or > 254 or 1 or 2 or 3) return "Invalid VK (left/right mouse buttons are forbidden)";
        if (b.Key is >= 0x10 and <= 0x12) return "Use a left/right modifier VK";
        var ownModifier = b.Key switch
        {
            0xA0 or 0xA1 => KeyModifiers.Shift,
            0xA2 or 0xA3 => KeyModifiers.Control,
            0xA4 or 0xA5 => KeyModifiers.Alt,
            0x5B or 0x5C => KeyModifiers.Windows,
            _ => KeyModifiers.None
        };
        if ((b.Modifiers & ownModifier) != 0) return "A modifier key cannot require its own modifier";
        if ((uint)b.Modifiers >= 16) return "Invalid modifiers";
        if (string.IsNullOrWhiteSpace(b.Context) || !contexts.ContainsContext(b.Context)) return "Unknown context";
        var hasCommand = !string.IsNullOrWhiteSpace(b.FeatureCommand);
        if ((b.Action is not null) == hasCommand) return "Choose an action or a feature command";
        if (b.Action is null)
            return commands.Contains(b.FeatureCommand!) ? null : "Unknown feature command";
        if (b.Action is ActionKind.PasteSearch or ActionKind.SetPriceNote or ActionKind.SetListingAmount)
            return "Reserved for Phase 2 feature manifests; not enabled for user bindings";
        if (!Enum.IsDefined(b.Action.Value)) return "Unknown action";
        if (b.Action is ActionKind.SendChatCommand or ActionKind.PasteSearch or ActionKind.SetPriceNote &&
            (string.IsNullOrWhiteSpace(b.Text) || b.Text.Any(c => char.IsControl(c) || c is '\u2028' or '\u2029')))
            return "Chat text must be nonempty and single line";
        if (b.Action is ActionKind.SendChatCommand or ActionKind.PasteSearch or ActionKind.SetPriceNote && b.Text!.Length > MaximumChatLength)
            return $"Chat text exceeds {MaximumChatLength} characters";
        return null;
    }
}
