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
    private GuideData? activeData;
    private string version = "";
    private byte[]? custom;
    private string? customId;
    private bool customDisabled;
    private GuideSave? lastSaved;
    public bool Visible { get { lock (sync) return visible; } }
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
            leagueStart = saved?.LeagueStart ?? true; bandit = saved?.Bandit ?? "none";
            customId = saved?.CustomId; customDisabled = saved?.CustomDisabled ?? false;
            if (customDisabled) notice = "Custom guide disabled after incompatibility; using default guide. Remove custom guide to discard it.";
            var customFailed = false;
            try
            {
                custom = customDisabled ? null : customId is not null ? store!.ReadCustom(customId) : saved?.Custom;
                progress = CreateProgress(custom is null ? data : GuideImport.Read(custom, data), leagueStart, bandit);
            }
            catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
            {
                customFailed = true; customDisabled = true; custom = null;
                progress = CreateProgress(data, leagueStart, bandit);
                notice = "Custom guide is incompatible; using default guide. Remove custom guide to discard it.";
            }
            if (!customFailed && saved is not null && saved.Version == version)
            {
                try { progress.Restore(saved.Index, saved.Times, saved.Complete, saved.Uncertain); }
                catch (ArgumentException) { store?.Preserve(); notice = "Saved progress is incompatible; using first step."; }
            }
            else if (!customFailed && saved is not null) notice = "Guide version changed; progress reset. Timers start paused.";
            // Migrate the old embedded payload once. Future checkpoints contain only its hash reference.
            if (custom is not null && customId is null && store is not null)
            {
                try { customId = store.WriteCustom(custom); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { store.Preserve(); }
            }
            lastSaved = saved?.Schema == 2 ? saved : saved is null ? new GuideSave(2, version, progress.Index,
                progress.ActMilliseconds.ToArray(), progress.Complete, progress.Uncertain, leagueStart, bandit, null) : null;
            savedAt = clock.Milliseconds;

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
    public event Action? VisibilityChanged;
    public void Toggle() { lock (sync) visible = !visible; VisibilityChanged?.Invoke(); }
    public void Hide()
    {
        lock (sync) { if (!visible) return; visible = false; }
        VisibilityChanged?.Invoke();
    }
    public void Import(byte[] bytes)
    {
        lock (sync)
        {
            if (pack is null) throw new InvalidOperationException("Guide pack is not available.");
            var parsed = GuideImport.Read(bytes, pack);
            var next = new GuideProgress(parsed, leagueStart, bandit);
            var id = store?.WriteCustom(bytes);
            activeData = parsed;
            customId = id; customDisabled = false; custom = bytes.ToArray(); progress = next; notice = "Custom guide imported; progress and timers reset."; Save(forceTimers: true);
        }
    }
    public void RemoveCustom()
    {
        lock (sync)
        {
            if (pack is null) return;
            var next = CreateProgress(pack, leagueStart, bandit);
            custom = null; customId = null; customDisabled = false; progress = next;
            notice = "Custom guide removed; default guide selected."; Save(forceTimers: true);
        }
    }
    public void Checkpoint()
    {
        lock (sync)
        {
            if (progress?.HasPendingTime(clock.Milliseconds) == true) progress.Tick(clock.Milliseconds);
            if (Save()) store?.RetryCleanup(customId);
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
            var next = CreateProgress(custom is null ? pack : GuideImport.Read(custom, pack), nextLeague, nextBandit);
            leagueStart = nextLeague; bandit = nextBandit; progress = next;
            notice = "Progress and timers reset."; Save(forceTimers: true);
        }
    }
    private GuideProgress CreateProgress(GuideData data, bool league, string choice)
    {
        var next = new GuideProgress(data, league, choice);
        activeData = data;
        return next;
    }
    public GuideSnapshot StructuredSnapshot()
    {
        lock (sync)
        {
            progress?.Tick(clock.Milliseconds);
            return GuideSnapshot.Create(visible, progress, activeData, pack, leagueStart, bandit, store?.Notice, notice) with
                { LeagueStart = leagueStart, Bandit = bandit, HasCustomGuide = custom is not null || customId is not null };
        }
    }
    public string Export() { lock (sync) { progress?.Tick(clock.Milliseconds); return progress?.Csv() ?? "act,seconds,uncertain\r\n"; } }
    private bool Save(bool forceTimers = false)
    {
        if (progress is null) return false;
        var state = new GuideSave(2, version, progress.Index, progress.ActMilliseconds.ToArray(), progress.Complete,
            progress.Uncertain, leagueStart, bandit, null, customId, customDisabled);
        if (lastSaved is not null && state with { Times = lastSaved.Times } == lastSaved &&
            (state.Times.SequenceEqual(lastSaved.Times) || !forceTimers && clock.Milliseconds - savedAt < 60000)) return true;
        try { store?.Save(state); lastSaved = state; savedAt = clock.Milliseconds; return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { notice = "Could not save guide progress."; return false; }
    }
}
