namespace ExileSight.Core.Logs.Redaction;

/// <summary>Session-scoped pseudonyms. Unknown lines are removed conservatively because they can contain
/// unrecognized account identifiers. Review excerpts before publishing; arbitrary prose is not safe metadata.</summary>
public sealed class LogRedactor(string? keep = null)
{
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);
    private readonly LogParser parser = new();

    private string Name(string name)
    {
        if (name == keep) return name;
        if (!names.TryGetValue(name, out var replacement))
        {
            var n = names.Count;
            var suffix = "";
            do { suffix = (char)('A' + n % 26) + suffix; n = n / 26 - 1; } while (n >= 0);
            names[name] = replacement = "Player" + suffix;
        }
        return replacement;
    }

    public string Redact(string line)
    {
        var e = parser.Parse(line);
        var prefix = e.Envelope is null ? "" : line[..(line.Length - e.Envelope.Message.Length)];
        var system = e.Envelope?.Message.StartsWith(": ", StringComparison.Ordinal) == true || line.StartsWith(": ", StringComparison.Ordinal) ? ": " : "";
        return prefix + (e switch
        {
            ChatMessage c => $"{(c.Channel == "Local" ? "" : c.Channel.StartsWith('@') ? c.Channel + " " : c.Channel)}{Name(c.Sender)}: {(c.Channel.StartsWith('@') ? "[whisper removed]" : "[chat removed]")}",
            CharacterLevelUp c => $"{system}{Name(c.Name)} ({c.Class}) is now level {c.Level}",
            PlayerSlain d => $"{system}{Name(d.Name)} has been slain.",
            PlayerDied d => $"{system}{Name(d.Name)} has committed suicide.",
            AfkModeChanged a => $"{system}AFK mode is now {(a.Enabled ? "ON" : "OFF")}.",
            DndChanged d => $"{system}DND mode is now {(d.Enabled ? "ON" : "OFF")}.",
            AreaGenerated or AreaEntered or MapEvent => e.Envelope?.Message ?? line,
            NpcDialogue d => $"{(EnglishPatterns.IsKnownNpc(d.Npc) ? d.Npc : Name(d.Npc))}: [dialogue removed]",
            FilterReloaded => "Finished reloading filter [name removed]",
            _ => "[unrecognized line removed]"
        });
    }
}
