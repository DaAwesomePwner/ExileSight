using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Input;

public sealed record BindingMapping(IReadOnlyList<HotkeyBinding> Bindings, IReadOnlyList<string> Rejected);
public static class BindingMapper
{
    public static BindingMapping Map(IEnumerable<PersistedBinding?> source, HotkeyConfiguration contexts,
        IEnumerable<IFeatureCommandHandler> handlers)
    {
        var commands = handlers.Select(h => h.Command).ToHashSet(StringComparer.Ordinal);
        var bindings = new List<HotkeyBinding>();
        var rejected = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var chords = new HashSet<(string, int, KeyModifiers)>();
        foreach (var b in source)
        {
            if (b is null) { rejected.Add("<null>: Binding required"); continue; }
            var error = Validate(b, contexts, commands);
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
                _ => null
            };
            bindings.Add(new(b.Id, b.Key, b.Modifiers, b.Block, action, b.Context, b.FeatureCommand));
        }
        return new(bindings.AsReadOnly(), rejected.AsReadOnly());
    }
    private static string? Validate(PersistedBinding b, HotkeyConfiguration contexts, HashSet<string> commands)
    {
        if (string.IsNullOrWhiteSpace(b.Id)) return "ID required";
        if (b.Key is < 1 or > 254 or 1 or 2 or 3) return "Invalid VK (left/right mouse buttons are forbidden)";
        if ((uint)b.Modifiers >= 16) return "Invalid modifiers";
        if (string.IsNullOrWhiteSpace(b.Context) || !contexts.ContainsContext(b.Context)) return "Unknown context";
        var hasCommand = !string.IsNullOrWhiteSpace(b.FeatureCommand);
        if ((b.Action is not null) == hasCommand) return "Choose an action or a feature command";
        if (b.Action is null)
            return commands.Contains(b.FeatureCommand!) ? null : "Unknown feature command";
        if (!Enum.IsDefined(b.Action.Value)) return "Unknown action";
        if (b.Action == ActionKind.SendChatCommand &&
            (string.IsNullOrWhiteSpace(b.Text) || b.Text.Any(c => char.IsControl(c) || c is '\u2028' or '\u2029')))
            return "Chat text must be nonempty and single line";
        return null;
    }
}
