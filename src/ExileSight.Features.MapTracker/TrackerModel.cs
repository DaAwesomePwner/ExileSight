using System.Globalization;
using ExileSight.Core.Features;
using ExileSight.Core.Logs;
namespace ExileSight.Features.MapTracker;

public sealed class TrackerModel(RunStore store)
{
    private readonly object sync = new();
    private readonly RunTracker tracker = new();
    private string league = "";
    public string League { get { lock (sync) return league; } }
    public string Status { get; private set; } = "Select a league to begin recording.";
    public string[] Leagues { get; set; } = [];
    public event Action<string>? LeagueSelected;
    public event Action? OpenRequested;
    public void Open() => OpenRequested?.Invoke();
    public void SelectLeague(string value)
    {
        lock (sync)
        {
            if (league == value) return;
            if (tracker.Accept(new(0, 0, new LogSourceChanged(), false), league) is { } old) Save(old);
            league = value;
            LeagueSelected?.Invoke(value);
            Status = "Recording. Ctrl+Shift+K requests /kills; take a start and end sample.";
        }
    }
    public void Copied(CopiedItem item) { lock (sync) tracker.Copied(item); }
    public void Accept(ClientLogEvent entry)
    {
        lock (sync)
        {
            if (league.Length == 0) return;
            if (tracker.Accept(entry, league) is { } completed) Save(completed);
            if (tracker.Current is { } current)
                Save(current with { Incomplete = true, Ended = entry.Timestamp ?? current.Started });
        }
    }
    private void Save(MapRun run)
    {
        try { store.Save(run); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        { Status = "Storage unavailable or newer schema (read-only): " + e.GetType().Name; }
    }
    public MapRun[] Query(string selectedLeague, string map = "", int? tier = null, DateTime? from = null, DateTime? to = null)
    {
        lock (sync) return store.Read(selectedLeague).Where(r => r.Map.Contains(map, StringComparison.OrdinalIgnoreCase)
            && (tier is null || r.Tier == tier) && (from is null || r.Started.Date >= from.Value.Date)
            && (to is null || r.Started.Date <= to.Value.Date)).OrderByDescending(r => r.Started).ToArray();
    }
    public void Notes(MapRun run, string notes)
    {
        lock (sync)
        {
            var bounded = notes[..Math.Min(notes.Length, 4096)];
            tracker.Notes(run.Id, bounded);
            Save((tracker.Current?.Id == run.Id ? tracker.Current with { Incomplete = true } : run) with { Notes = bounded });
        }
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
                r.Deaths.ToString(CultureInfo.InvariantCulture), r.Kills?.ToString(CultureInfo.InvariantCulture) ?? "",
                string.Join("; ", r.Mechanics), string.Join("; ", r.Mods), r.Notes, r.Incomplete.ToString() }.Select(Q))));
    }
}
