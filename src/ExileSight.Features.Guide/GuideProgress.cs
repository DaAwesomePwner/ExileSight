// Behaviour reference: Exile-UI modules/leveling tracker.ahk (MIT). Independent implementation.
using System.Globalization;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Logs;

namespace ExileSight.Features.Guide;

public sealed class GuideProgress
{
    private readonly GuideData data;
    private readonly GuidePageData[] pages;
    private long? lastTick, lastActivity;
    private long graceUntil;
    private string? generated;
    private long generatedAt;
    public int Index { get; private set; }
    public int Level { get; private set; }
    public bool Uncertain { get; private set; }
    public bool Complete { get; private set; }
    public long[] ActMilliseconds { get; } = new long[11];
    public GuidePageData Current => pages[Index];
    public GuidePageData? Next => Index + 1 < pages.Length ? pages[Index + 1] : null;
    public const long IdleMilliseconds = 120_000;
    public GuideProgress(GuideData data, bool leagueStart = true, string bandit = "none")
    {
        this.data = data;
        pages = data.Pages.Where(p => p.Condition is null || p.Condition.Kind switch
        {
            "league-start" => p.Condition.Values.Contains(leagueStart ? "yes" : "no"),
            "bandit" => p.Condition.Values.Contains(bandit), _ => false
        }).ToArray();
        if (pages.Length == 0) throw new ArgumentException("Guide has no applicable pages.");
    }
    public void Restore(int index, long[] times, bool complete, bool uncertain)
    {
        if (index < 0 || index >= pages.Length || times.Length != 11 || times.Any(t => t < 0 || t > 315360000000L))
            throw new ArgumentException("Invalid progress.");
        Index = index; Array.Copy(times, ActMilliseconds, 11); Complete = complete; Uncertain = uncertain;
    }
    public bool HasPendingTime(long now) => !Complete && lastTick is { } tick && lastActivity is { } activity &&
        tick < Math.Min(now, activity + IdleMilliseconds);
    public void Tick(long now)
    {
        if (lastTick is { } tick && lastActivity is { } activity && !Complete)
        {
            var delta = Math.Max(0, Math.Min(now, activity + IdleMilliseconds) - tick);
            if (Current.Act is >= 1 and <= 10)
                ActMilliseconds[Current.Act] = Math.Min(315360000000L, ActMilliseconds[Current.Act] + delta);
        }
        lastTick = now;
    }
    public void Navigate(int delta, long now)
    {
        Tick(now); lastActivity = now; generated = null;
        Complete = delta > 0 && Index == pages.Length - 1;
        Index = Math.Clamp(Index + delta, 0, pages.Length - 1); graceUntil = now + 2000;
    }
    public void Accept(ClientLogEvent entry, long now)
    {
        if (entry.DroppedBefore > 0 || entry.Event is LogSourceChanged)
        {
            Uncertain = true; generated = null; lastTick = now; lastActivity = null; return;
        }
        Tick(now);
        switch (entry.Event)
        {
            case AfkModeChanged afk when afk.Enabled: lastActivity = null; break;
            case CharacterLevelUp level: Level = level.Level; lastActivity = now; break;
            case AreaGenerated area: generated = area.AreaId; generatedAt = now; break;
            case AreaEntered area:
                lastActivity = now;
                var exact = generated is not null && now - generatedAt is >= 0 and <= 30000;
                var references = Current.Lines.Where(l => !l.Text.Contains("(hint)_", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(l => l.AreaIds).ToHashSet(StringComparer.Ordinal);
                var candidates = data.Areas.Where(a => exact ? a.Id == generated :
                    a.Name == area.Name && references.Contains(a.Id)).ToArray();
                if (!exact && candidates.Length > 1) candidates = candidates.Where(a => a.Act == Current.Act).ToArray();
                generated = null;
                if (Complete || now < graceUntil || candidates.Length != 1) return;
                var target = candidates[0];
                if (target.Act is < 1 or > 10 || target.Id.Contains("hideout", StringComparison.OrdinalIgnoreCase)) return;
                var last = Current.Lines.Where(l => !l.Text.Contains("(hint)_", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(l => l.AreaIds).LastOrDefault();
                if (last != target.Id) return;
                if (Index + 1 < pages.Length) Index++; else Complete = true;
                break;
        }
    }
    public string Csv() => "act,seconds,uncertain\r\n" + string.Join("", Enumerable.Range(1, 10).Select(a =>
        $"{a},{(ActMilliseconds[a] / 1000d).ToString("F3", CultureInfo.InvariantCulture)},{Uncertain.ToString().ToLowerInvariant()}\r\n")) +
        $"total,{(ActMilliseconds.Sum() / 1000d).ToString("F3", CultureInfo.InvariantCulture)},{Uncertain.ToString().ToLowerInvariant()}\r\n";
}
