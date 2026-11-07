namespace ExileSight.Core.Logs;

internal static class TradeWhisperParser
{
    internal static TradeWhisper? Parse(string raw, LogEnvelope? envelope, string channel, string peer, string text)
    {
        if (!channel.StartsWith('@')) return null;
        var match = LogParser.Match(text, @"^(?:Hi, )?(?:I would|I'd) like to buy your (.+)$");
        if (!match.Success) return null;
        var body = match.Groups[1].Value;
        string? stash = null;
        var stashStart = body.IndexOf(" (stash tab ", StringComparison.Ordinal);
        if (stashStart >= 0)
        {
            var close = body.IndexOf(')', stashStart);
            if (close < 0) return null;
            stash = body[(stashStart + 2)..close];
            body = body[..stashStart];
        }
        var leagueStart = body.LastIndexOf(" in ", StringComparison.Ordinal);
        if (leagueStart < 0) return null;
        var league = body[(leagueStart + 4)..].TrimEnd('.');
        var item = body[..leagueStart];
        string? price = null;
        foreach (var separator in new[] { " listed for ", " for my " })
        {
            var index = item.IndexOf(separator, StringComparison.Ordinal);
            if (index < 0) continue;
            price = item[(index + separator.Length)..];
            item = item[..index];
            break;
        }
        return item.Length == 0 || league.Length == 0 || price == "" ? null
            : new TradeWhisper(raw, envelope, channel, peer, text, item, price, league, stash);
    }
}
