namespace ExileSight.Core.Setup;

/// <summary>Session shutdown must never launch an installer. The staged update stays on disk.</summary>
public sealed class ExitPolicy
{
    private int sessionEnding;
    public bool IsSessionEnding => Volatile.Read(ref sessionEnding) != 0;
    public bool ApplyUpdateOnExit => !IsSessionEnding;
    public TimeSpan StopTimeout => TimeSpan.FromSeconds(IsSessionEnding ? 1 : 5);
    public void BeginSessionEnding() => Interlocked.Exchange(ref sessionEnding, 1);
}
