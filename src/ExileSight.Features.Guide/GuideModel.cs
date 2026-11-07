using ExileSight.Core.DataPacks;
using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Features.Guide;

public sealed class GuideModel(GuideStore? store = null, IClock? clock = null)
{
    private readonly object sync = new();
    private readonly IClock clock = clock ?? new SystemClock();
    private GuideData? pack;
    private GuideProgress? progress;
    private string version = "";
    private byte[]? custom;
    private bool leagueStart = true, visible;
    private string bandit = "none";
    private string? notice;
    private long savedAt;
    public void Load(GuideData data, string packVersion)
    {
        lock (sync)
        {
            // Freeze the parsed version for this run: a refresh cannot reinterpret a saved page index.
            if (pack is not null) return;
            pack = data; version = packVersion;
            var saved = store?.Load();
            try
            {
                if (saved is not null)
                { leagueStart = saved.LeagueStart; bandit = saved.Bandit; custom = saved.Custom; }
                progress = new(custom is null ? data : GuideImport.Read(custom, data), leagueStart, bandit);
                if (saved is not null && saved.Version == version)
                    progress.Restore(saved.Index, saved.Times, saved.Complete, saved.Uncertain);
                else if (saved is not null) notice = "Guide version changed; progress reset. Timers start paused.";
            }
            catch (ArgumentException)
            { store?.Preserve(); custom = null; progress = new(data); notice = "Saved guide is incompatible; using default guide."; }
        }
    }
    public void Accept(ClientLogEvent entry)
    {
        lock (sync)
        {
            progress?.Accept(entry, clock.Milliseconds);
            if (entry.DroppedBefore > 0 || entry.Event is AreaEntered or CharacterLevelUp or LogSourceChanged or AfkModeChanged) Save();
        }
    }
    public void Navigate(int delta)
    {
        lock (sync) { progress?.Navigate(delta, clock.Milliseconds); Save(); }
    }
    public void Toggle() { lock (sync) visible = !visible; }
    public void Hide() { lock (sync) visible = false; }
    public void Import(byte[] bytes)
    {
        lock (sync)
        {
            if (pack is null) throw new InvalidOperationException("Guide pack is not available.");
            var parsed = GuideImport.Read(bytes, pack);
            var next = new GuideProgress(parsed, leagueStart, bandit);
            custom = bytes.ToArray(); progress = next; notice = "Custom guide imported; progress and timers reset."; Save();
        }
    }
    public void Reset(bool changeLeague = false, bool changeBandit = false)
    {
        lock (sync)
        {
            if (pack is null) return;
            var nextLeague = changeLeague ? !leagueStart : leagueStart;
            var nextBandit = bandit;
            if (changeBandit)
            {
                string[] choices = ["none", "oak", "alira", "kraityn"];
                nextBandit = choices[(Array.IndexOf(choices, bandit) + 1) % choices.Length];
            }
            var next = new GuideProgress(custom is null ? pack : GuideImport.Read(custom, pack), nextLeague, nextBandit);
            leagueStart = nextLeague; bandit = nextBandit; progress = next;
            notice = "Progress and timers reset."; Save();
        }
    }
    public (bool Visible, string Text) Snapshot()
    {
        lock (sync)
        {
            progress?.Tick(clock.Milliseconds);
            if (clock.Milliseconds - savedAt >= 5000) { Save(); savedAt = clock.Milliseconds; }
            var header = "Single default profile — shared across all characters. Reset for a new run.\n" +
                $"League start: {(leagueStart ? "yes" : "no")} · Bandit: {bandit}\n";
            if (progress is null) return (visible, header + "Waiting for poe1/guide pack.");
            string Display(GuidePageData page) => string.Join("\n", page.Lines.Select(l => GuideDisplay.Line(l.Text, pack!)));
            var elapsed = TimeSpan.FromMilliseconds(progress.ActMilliseconds.Sum());
            var actTime = TimeSpan.FromMilliseconds(progress.ActMilliseconds[progress.Current.Act]);
            return (visible, header + $"Act {progress.Current.Act} · Level {progress.Level} · Act {actTime:hh\\:mm\\:ss} · Total {elapsed:hh\\:mm\\:ss}\n" +
                "Timers pause after 2 minutes without zone/level/manual activity.\n" +
                (progress.Uncertain ? "Timing uncertain: log gap/source change.\n" : "") +
                (progress.Complete ? "Guide complete\n" : "Current\n") + Display(progress.Current) +
                "\n\nNext\n" + (progress.Next is { } next ? Display(next) : "End of guide") + "\n" + (store?.Notice ?? notice));
        }
    }
    public string Export() { lock (sync) { progress?.Tick(clock.Milliseconds); return progress?.Csv() ?? "act,seconds,uncertain\r\n"; } }
    private void Save()
    {
        if (progress is null) return;
        try { store?.Save(new(1, version, progress.Index, progress.ActMilliseconds.ToArray(), progress.Complete,
            progress.Uncertain, leagueStart, bandit, custom)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { notice = "Could not save guide progress."; }
    }
}
