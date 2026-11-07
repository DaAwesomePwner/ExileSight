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
        // Chat must be classified before system messages to prevent text masquerading as events.
        m = Match(text, @"^(@From |@To |[#%&$])(?:<[^>]*> )?([^:]+):\s?(.*)$");
        if (m.Success)
        {
            var channel = m.Groups[1].Value.Trim();
            var peer = m.Groups[2].Value;
            var body = m.Groups[3].Value;
            var trade = Match(body, @"^Hi, I would like to buy your (.+?) listed for (.+?) in (.+?)(?: \((stash tab .+)\))?$");
            if (channel.StartsWith('@') && trade.Success)
                return new TradeWhisper(raw, envelope, channel, peer, body, trade.Groups[1].Value,
                    trade.Groups[2].Value, trade.Groups[3].Value, trade.Groups[4].Success ? trade.Groups[4].Value : null);
            return new ChatMessage(raw, envelope, channel, peer, body);
        }
        if (text.StartsWith(": ", StringComparison.Ordinal)) text = text[2..];
        m = Match(text, "^Generating level (\\d+) area \"([^\"]+)\" with seed (\\d+)$");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var areaLevel) && long.TryParse(m.Groups[3].Value, out var seed))
            return new AreaGenerated(raw, envelope, areaLevel, m.Groups[2].Value, seed);
        m = Match(text, @"^You have entered (.+)\.$");
        if (m.Success) return new AreaEntered(raw, envelope, m.Groups[1].Value);
        m = Match(text, @"^(.+?) \(([^)]+)\) is now level (\d+)\.?$");
        if (m.Success && int.TryParse(m.Groups[3].Value, out var level))
            return new CharacterLevelUp(raw, envelope, m.Groups[1].Value, m.Groups[2].Value, level);
        m = Match(text, @"^(.+?) has (been slain|committed suicide)\.$");
        if (m.Success) return m.Groups[2].Value == "been slain" ? new PlayerSlain(raw, envelope, m.Groups[1].Value)
            : new PlayerDied(raw, envelope, m.Groups[1].Value);
        m = Match(text, @"^Finished reloading (?:online )?filter (.+)$");
        if (m.Success) return new FilterReloaded(raw, envelope, m.Groups[1].Value);
        m = Match(text, @"^(AFK|DND) mode is now (ON|OFF)\.(.*)$");
        if (m.Success) return m.Groups[1].Value == "AFK" ? new AfkModeChanged(raw, envelope, m.Groups[2].Value == "ON")
            : new DndChanged(raw, envelope, m.Groups[2].Value == "ON");
        if (text.StartsWith("Spawning discoverable Hideout", StringComparison.Ordinal)) return new MapEvent(raw, envelope, "Hideout");
        if (text == "The Nameless Seer has appeared nearby") return new MapEvent(raw, envelope, "Seer");
        if (text == "A Reflecting Mist has manifested nearby") return new MapEvent(raw, envelope, "ReflectingMist");
        m = Match(text, @"^([^:]+): (.*)$");
        if (m.Success)
        {
            var npc = m.Groups[1].Value;
            var body = m.Groups[2].Value;
            if (EnglishPatterns.MercenaryGreetings.Contains(body)) return new MercenaryEncounter(raw, envelope, npc, body);
            if (EnglishPatterns.Npcs.Contains(npc)) return new NpcDialogue(raw, envelope, npc, body);
            return new ChatMessage(raw, envelope, "Local", npc, body);
        }
        return new Unknown(raw, envelope);
    }
}
