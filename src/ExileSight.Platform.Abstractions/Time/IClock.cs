namespace ExileSight.Platform.Abstractions.Time;

public interface IClock
{
    long Milliseconds { get; }
    DateTimeOffset UtcNow { get; }
    void Delay(int milliseconds);
}
public sealed class SystemClock : IClock
{
    public void Delay(int milliseconds) => Thread.Sleep(milliseconds);
    public long Milliseconds => Environment.TickCount64;
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
