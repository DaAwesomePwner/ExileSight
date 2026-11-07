using System.Globalization;
using System.Text.RegularExpressions;

namespace ExileSight.Core.Logs;

// English behaviour derived from Exile-UI (MIT), modules/client log.ahk and data/english/client.txt.
public sealed class LogParser
{
    internal static Match Match(string text, string pattern) => Regex.Match(text, pattern,
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));

    public LogEvent Parse(string? line)
    {
        var raw = line ?? "";
        try { return ParseCore(raw); }
        catch (RegexMatchTimeoutException) { return new Unknown(raw, null); }
    }

    private static LogEvent ParseCore(string raw)
    {
        var text = raw.TrimStart('\uFEFF');
        LogEnvelope? envelope = null;
        var m = Match(text, @"^(\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}) (\d+) (\S+) \[(\w+) ([^\]]+)\] (.*)$");
        if (m.Success)
        {
            if (!DateTime.TryParseExact(m.Groups[1].Value, "yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var timestamp) || !long.TryParse(m.Groups[2].Value, out var uptime)) return new Unknown(raw, null);
            text = m.Groups[6].Value;
            envelope = new(timestamp, uptime, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value, text);
        }
        // Network endpoints contain colons, but are not speaker delimiters. Keep diagnostic
        // recognition narrow and source-qualified so player text cannot suppress real chat.
        if (envelope?.Source.StartsWith("Client ", StringComparison.Ordinal) == true
            && Match(text, @"^(?:Connecting to (?:instance|login) server at \S+|Connected to \S+ in \d+ms\.?)$").Success)
            return new Unknown(raw, envelope);
        var system = text.StartsWith(": ", StringComparison.Ordinal);
        if (!system)
        {
            m = Match(text, @"^(@From |@To |[#%&$])?(?:<[^>]*> )?([^:]+):\s?(.*)$");
            if (m.Success)
            {
                var channel = m.Groups[1].Success ? m.Groups[1].Value.Trim() : "Local";
                var speaker = m.Groups[2].Value;
                var body = m.Groups[3].Value;
                if (channel == "Local" && EnglishPatterns.IsKnownNpc(speaker))
                {
                    if (EnglishPatterns.IsMercenary(speaker) && EnglishPatterns.MercenaryGreetings.Any(greeting => body.Contains(greeting, StringComparison.Ordinal)))
                        return new MercenaryEncounter(raw, envelope, speaker, body);
                    return new NpcDialogue(raw, envelope, speaker, body);
                }
                return TradeWhisperParser.Parse(raw, envelope, channel, speaker, body)
                    ?? new ChatMessage(raw, envelope, channel, speaker, body);
            }
        }
        else text = text[2..];
        // These are client diagnostics, not chat-system messages, and have no ': ' prefix.
        m = Match(text, "^Generating level (\\d+) area \"([^\"]+)\" with seed (\\d+)$");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var areaLevel) && long.TryParse(m.Groups[3].Value, out var seed))
            return new AreaGenerated(raw, envelope, areaLevel, m.Groups[2].Value, seed);
        m = Match(text, @"Finished reloading (?:online )?filter (.+)$");
        if (m.Success) return new FilterReloaded(raw, envelope, m.Groups[1].Value);
        if (text.Contains("Spawning discoverable Hideout", StringComparison.Ordinal)) return new MapEvent(raw, envelope, "Hideout");
        if (!system) return new Unknown(raw, envelope);
        m = Match(text, @"^You have entered (.+)\.$");
        if (m.Success) return new AreaEntered(raw, envelope, m.Groups[1].Value);
        m = Match(text, @"^(.+?) \(([^)]+)\) is now level (\d+)\.?$");
        if (m.Success && int.TryParse(m.Groups[3].Value, out var level))
            return new CharacterLevelUp(raw, envelope, m.Groups[1].Value, m.Groups[2].Value, level);
        m = Match(text, @"^(.+?) has (been slain|committed suicide)\.$");
        if (m.Success) return m.Groups[2].Value == "been slain" ? new PlayerSlain(raw, envelope, m.Groups[1].Value)
            : new PlayerDied(raw, envelope, m.Groups[1].Value);
        m = Match(text, @"^(AFK|DND) mode is now (ON|OFF)\.(.*)$");
        if (m.Success) return m.Groups[1].Value == "AFK" ? new AfkModeChanged(raw, envelope, m.Groups[2].Value == "ON")
            : new DndChanged(raw, envelope, m.Groups[2].Value == "ON");
        if (text.Contains("The Nameless Seer has appeared nearby", StringComparison.Ordinal)) return new MapEvent(raw, envelope, "Seer");
        if (text.Contains("A Reflecting Mist has manifested nearby", StringComparison.Ordinal)) return new MapEvent(raw, envelope, "ReflectingMist");
        return new Unknown(raw, envelope);
    }
}
