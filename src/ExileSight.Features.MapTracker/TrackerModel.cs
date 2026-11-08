using System.Globalization;
using ExileSight.Core.Features;
using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Time;
namespace ExileSight.Features.MapTracker;

public sealed class TrackerModel(IRunStore store, IClock? suppliedClock = null, Action<string>? reportFailure = null)
{
    private readonly object sync = new();
    private readonly RunTracker tracker = new();
    private readonly RunWriter writer = new(store, reportFailure);
    private readonly IClock clock = suppliedClock ?? new SystemClock();
    private long lastReceipt;
    private bool extrapolate, stopped;
    private Task? stopping;
    private double checkpointSeconds;
    private string league = "";
    public string League { get { lock (sync) return league; } }
    private string status = "Select a league to begin recording.";
    public string Status
    {
        get { lock (sync) return (writer.Failure(league) ?? status) +
            (tracker.Character is { } name ? " Deaths: counting for " + name : " Deaths: not counted — confirm character") +
            (tracker.IdentityNotice is { } notice ? " · " + notice : ""); }
    }
    public Task FlushAsync() => writer.FlushAsync();
    public string? Reconfirm { get { lock (sync) return tracker.Reconfirm; } }
    public string[] Candidates { get { lock (sync) return tracker.Candidates; } }
    public bool ConfirmCharacter(string name)
    {
        lock (sync)
        {
            if (stopped || !tracker.ConfirmCharacter(name)) return false;
            if (tracker.Current is { } current) Save(current with { Incomplete = true });
            return true;
        }
    }
    public Task StopAsync()
    {
        lock (sync) return stopping ??= StopCoreAsync();
    }
    private async Task StopCoreAsync()
    {
        // Freeze the reducer before draining, so late log callbacks cannot race the final write.
        stopped = true;
        var elapsed = extrapolate ? Math.Clamp((clock.Milliseconds - lastReceipt) / 1000d, 0, RunTracker.CheckpointWindowSeconds) : 0;
        await writer.FlushAsync().ConfigureAwait(false);
        lock (sync)
            if (tracker.FinalCheckpoint(elapsed) is { } final) Save(final);
        await writer.FlushAsync().ConfigureAwait(false);
    }
    public event Action<string>? LeagueSelected;
    public event Action? OpenRequested;
    public void Open() => OpenRequested?.Invoke();
    public void SelectLeague(string value)
    {
        lock (sync)
        {
            if (stopped || league == value) return;
            if (league.Length > 0 && tracker.Accept(new(0, 0, new LogSourceChanged(), false), league) is { } old) Save(old);
            league = value;
            LeagueSelected?.Invoke(value);
            status = "Recording. Ctrl+Shift+K requests /kills; take a start and end sample.";
        }
    }
    public void Copied(CopiedItem item) { lock (sync) if (!stopped) tracker.Copied(item); }
    public void Accept(ClientLogEvent entry)
    {
        lock (sync)
        {
            if (stopped || (league.Length == 0 && entry.Event is AreaGenerated)) return;
            if (entry.Event.Envelope is not null)
            { lastReceipt = clock.Milliseconds; extrapolate = !entry.IsReplay; }
            var revision = tracker.Revision;
            var previousRun = tracker.Current?.Id;
            if (tracker.Accept(entry, league) is { } completed) Save(completed);
            if (tracker.Current?.Id != previousRun) checkpointSeconds = 0;
            if (tracker.Current is { } current &&
                (revision != tracker.Revision || current.Seconds - checkpointSeconds >= RunTracker.CheckpointWindowSeconds))
            {
                Save(current with { Incomplete = true, Ended = entry.Timestamp ?? current.Started });
                checkpointSeconds = current.Seconds;
            }
        }
    }
    private void Save(MapRun run) => writer.Enqueue(run);
    public MapRun[] Query(string selectedLeague, string map = "", int? tier = null, DateTime? from = null, DateTime? to = null)
    {
        return writer.Read(selectedLeague).Where(r => r.Map.Contains(map, StringComparison.OrdinalIgnoreCase)
            && (tier is null || r.Tier == tier) && (from is null || r.Started.Date >= from.Value.Date)
            && (to is null || r.Started.Date <= to.Value.Date)).OrderByDescending(r => r.Started).ToArray();
    }
    public void Notes(MapRun run, string notes)
    {
        lock (sync)
        {
            if (stopped) return;
            var bounded = notes[..Math.Min(notes.Length, 4096)];
            tracker.Notes(run.Id, bounded);
            Save((tracker.Current?.Id == run.Id ? tracker.Current with { Incomplete = true } : run) with { Notes = bounded });
        }
    }
    public static string DeathTotals(IEnumerable<MapRun> runs)
    {
        var rows = runs.ToArray(); var unknown = rows.Count(r => !r.DeathsKnown);
        var known = rows.Where(r => r.DeathsKnown).Sum(r => r.Deaths);
        return unknown == 0 ? $"{known} deaths" : $"Deaths: unknown ({known} known; {unknown} runs unknown)";
    }
    public static string Csv(IEnumerable<MapRun> runs)
    {
        static string Q(string value)
        {
            // Prevent spreadsheet formula execution, including after whitespace/control characters.
            if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+')
                || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return "League,Map,Tier,Started,Seconds,Portals,Deaths,Kills,Mechanics,Mods,Notes,Incomplete\r\n" +
            string.Join("\r\n", runs.Select(r => string.Join(',', new[] { r.League, r.Map,
                r.Tier.ToString(CultureInfo.InvariantCulture), r.Started.ToString("O"),
                r.Seconds.ToString(CultureInfo.InvariantCulture), r.Portals.ToString(CultureInfo.InvariantCulture),
                r.DeathsKnown ? r.Deaths.ToString(CultureInfo.InvariantCulture) : "", r.Kills?.ToString(CultureInfo.InvariantCulture) ?? "",
                string.Join("; ", r.Mechanics), string.Join("; ", r.Mods), r.Notes, r.Incomplete.ToString() }.Select(Q))));
    }
}
