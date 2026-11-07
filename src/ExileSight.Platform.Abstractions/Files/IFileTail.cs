namespace ExileSight.Platform.Abstractions.Files;

public interface IFileTail
{
    /// <summary>Null position starts at EOF; otherwise position is a UTF-8 byte boundary.
    /// Truncation or detected replacement restarts at zero. Only newline-terminated lines are emitted.
    /// Cancellation throws OperationCanceledException; consumers own enumeration lifetime.</summary>
    IAsyncEnumerable<string> ReadLinesAsync(string path, long? position = null, CancellationToken cancellationToken = default);
}
