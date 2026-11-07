using System.Runtime.CompilerServices;
using ExileSight.Platform.Abstractions.Files;

namespace ExileSight.Core.Logs.Tail;

/// <summary>Read-only polling with handle-based creation time and consumed-byte checkpoints.
/// Indistinguishable replacements (same creation time and checkpoint) cannot be guaranteed detectable.
/// Each poll reads at most 8 KiB. Lines exceeding 64 Ki UTF-16 characters are discarded, with a diagnostic.
/// Missing files and I/O failures retry with capped exponential backoff; cancellation remains immediate.</summary>
public sealed class PollingFileTail(IFileTailTimer? timer = null, int pollingMilliseconds = 350,
    Action<string>? warning = null) : IFileTail
{
    public async IAsyncEnumerable<string> ReadLinesAsync(string path, long? position = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position ?? 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(pollingMilliseconds, 1);
        var reader = new TailReader(position, warning ?? (message => System.Diagnostics.Trace.TraceWarning(message)));
        var delay = timer ?? new AsyncTimer();
        var failures = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> lines = [];
            var caughtUp = true;
            try
            {
                (lines, caughtUp) = await reader.ReadAsync(path, cancellationToken).ConfigureAwait(false);
                failures = 0;
            }
            catch (IOException)
            {
                failures = Math.Min(failures + 1, 5);
                // Do not log a path or exception message: it may contain account information.
                warning?.Invoke("Log temporarily unavailable; retrying.");
            }
            foreach (var line in lines) yield return line;
            if (!caughtUp) continue;
            var milliseconds = failures == 0 ? pollingMilliseconds
                : (int)Math.Min(5000L, (long)pollingMilliseconds * (1 << failures));
            await delay.DelayAsync(milliseconds, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class AsyncTimer : IFileTailTimer
    {
        public Task DelayAsync(int milliseconds, CancellationToken cancellationToken) => Task.Delay(milliseconds, cancellationToken);
    }
}
