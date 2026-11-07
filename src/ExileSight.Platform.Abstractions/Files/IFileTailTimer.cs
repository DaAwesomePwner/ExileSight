namespace ExileSight.Platform.Abstractions.Files;

/// <summary>Cancellable asynchronous polling delay; implementations must not block a worker thread.</summary>
public interface IFileTailTimer
{
    Task DelayAsync(int milliseconds, CancellationToken cancellationToken);
}
