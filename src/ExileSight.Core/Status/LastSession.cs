namespace ExileSight.Core.Status;

public sealed record SessionMap(DateTimeOffset Started, DateTimeOffset Ended, double Seconds, int? Deaths);
public sealed record SessionSale(DateTimeOffset? Sold, decimal Amount, string Currency, bool Purchase = false);
public sealed record LastSession(DateTimeOffset Started, DateTimeOffset Ended, IReadOnlyList<SessionMap> Maps,
    double AverageSeconds, int KnownDeaths, bool DeathsKnown, decimal DivineSold)
{
    public static LastSession? Map(IEnumerable<SessionMap> runs, IEnumerable<SessionSale> sales)
    {
        var ordered = runs.OrderBy(r => r.Started).ToArray();
        if (ordered.Length == 0) return null;
        var start = 0;
        var end = ordered[0].Ended;
        for (var i = 1; i < ordered.Length; i++)
        {
            if (ordered[i].Started - end > TimeSpan.FromHours(1)) start = i;
            end = ordered[i].Ended > end ? ordered[i].Ended : end;
        }
        var maps = ordered[start..];
        var from = maps[0].Started;
        return new(from, end, maps, maps.Average(r => r.Seconds), maps.Sum(r => r.Deaths ?? 0),
            maps.All(r => r.Deaths.HasValue), sales.Where(s => !s.Purchase && s.Currency == "divine" &&
                s.Sold >= from && s.Sold <= end).Sum(s => s.Amount));
    }
}
