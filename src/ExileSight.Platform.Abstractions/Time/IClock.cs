namespace ExileSight.Platform.Abstractions.Time;

public interface IClock
{
    long Milliseconds { get; }
    DateTimeOffset UtcNow { get; }
}
public sealed class SystemClock : IClock
{
    public long Milliseconds => Environment.TickCount64;
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
