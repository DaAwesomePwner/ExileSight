// Behaviour reference: Exile-UI modules/qol tools.ahk ~88–380 (MIT); no game actions.
namespace ExileSight.Features.Timers;

public enum TimerKind { Countdown, Stopwatch }

/// <summary>UTC wall time includes time while the app is closed. Backward jumps clamp a countdown
/// to its saved remaining value and stopwatch increments to zero; forward jumps count as elapsed.
/// Stopwatch display is bounded to ten years. Pausing checkpoints the displayed value.</summary>
public sealed record TimerState(Guid Id, string Name, TimerKind Kind, TimeSpan Duration,
    TimeSpan SavedValue, DateTimeOffset? EndUtc, DateTimeOffset? RunningSinceUtc, bool Alert, bool Alerted)
{
    public static readonly TimeSpan Maximum = TimeSpan.FromDays(3650);
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRunning => EndUtc is not null || RunningSinceUtc is not null;
    public static TimerState Create(string name, TimerKind kind, TimeSpan duration, bool alert)
    {
        var row = new TimerState(Guid.NewGuid(), Clean(name), kind, kind == TimerKind.Countdown ? duration : TimeSpan.Zero,
            kind == TimerKind.Countdown ? duration : TimeSpan.Zero, null, null, alert, false);
        if (!row.IsValid()) throw new ArgumentException("Use a name and a countdown between zero and ten years.");
        return row;
    }
    public TimeSpan Value(DateTimeOffset now) => Kind == TimerKind.Countdown
        ? EndUtc is { } end ? Clamp(end - now, SavedValue) : SavedValue
        : RunningSinceUtc is { } since ? SavedValue + Clamp(now - since, Maximum - SavedValue) : SavedValue;
    public TimerState Start(DateTimeOffset now)
    {
        if (IsRunning || (Kind == TimerKind.Countdown && SavedValue == TimeSpan.Zero)) return this;
        return Kind == TimerKind.Countdown
            ? this with { EndUtc = now.ToUniversalTime().Add(SavedValue) }
            : this with { RunningSinceUtc = now.ToUniversalTime() };
    }
    // An expired countdown pauses at zero; Complete still persists and alerts on the next tick.
    public TimerState Pause(DateTimeOffset now) => this with { SavedValue = Value(now), EndUtc = null, RunningSinceUtc = null };
    public TimerState Reset() => this with { SavedValue = Duration, EndUtc = null, RunningSinceUtc = null, Alerted = false };
    public TimerState Complete(DateTimeOffset now) => Kind == TimerKind.Countdown && !Alerted && Value(now) == TimeSpan.Zero
        ? this with { SavedValue = TimeSpan.Zero, EndUtc = null, Alerted = true } : this;
    public TimerState Rename(string name)
    {
        var clean = Clean(name);
        if (clean.Length == 0) throw new ArgumentException("A name is required.");
        return this with { Name = clean };
    }
    public bool IsValid() => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 80 &&
        Name == Clean(Name) && Enum.IsDefined(Kind) && Duration >= TimeSpan.Zero && Duration <= Maximum &&
        SavedValue >= TimeSpan.Zero && SavedValue <= Maximum &&
        (Kind == TimerKind.Countdown ? Duration > TimeSpan.Zero && SavedValue <= Duration && RunningSinceUtc is null &&
            (!Alerted || SavedValue == TimeSpan.Zero && EndUtc is null) && (EndUtc is null || SavedValue > TimeSpan.Zero)
            : Duration == TimeSpan.Zero && EndUtc is null && !Alerted) &&
        (EndUtc is null || EndUtc.Value.Offset == TimeSpan.Zero) &&
        (RunningSinceUtc is null || RunningSinceUtc.Value.Offset == TimeSpan.Zero);
    private static string Clean(string name) => new(name.Trim().Where(c => !char.IsControl(c)).Take(80).ToArray());
    private static TimeSpan Clamp(TimeSpan value, TimeSpan max) => value < TimeSpan.Zero ? TimeSpan.Zero : value > max ? max : value;
}
