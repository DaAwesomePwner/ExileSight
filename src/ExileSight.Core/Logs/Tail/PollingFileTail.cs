using System.Runtime.CompilerServices;
using ExileSight.Platform.Abstractions.Files;

namespace ExileSight.Core.Logs.Tail;

/// <summary>Read-only polling. One handle drains the backlog in bounded batches until EOF.
/// Indistinguishable replacements cannot be guaranteed detectable without a stable native file ID.
/// Lines exceeding 64 Ki UTF-16 characters are discarded. I/O/access failures retry with capped
/// backoff. Diagnostics are limited to one per condition per minute, per enumeration.</summary>
public sealed class PollingFileTail(IFileTailTimer? timer = null, int pollingMilliseconds = 350,
    Action<string>? warning = null, IFileTailIdentityProvider? identityProvider = null,
    TimeProvider? timeProvider = null) : IFileTail, IFileTailDiagnostics
{
    public event Action<FileTailDiagnostic>? Diagnostic;

    public async IAsyncEnumerable<string> ReadLinesAsync(string path, long? position = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position ?? 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(pollingMilliseconds, 1);
        var clock = timeProvider ?? TimeProvider.System;
        var reported = new Dictionary<FileTailCondition, long>();
        void Report(FileTailCondition condition)
        {
            var now = clock.GetTimestamp();
            if (reported.TryGetValue(condition, out var previous)
                && clock.GetElapsedTime(previous, now) < TimeSpan.FromMinutes(1)) return;
            reported[condition] = now;
            var message = condition switch
            {
                FileTailCondition.OversizedLine => "Discarded log line exceeding 65536 characters.",
                FileTailCondition.AccessDenied => "Log access temporarily denied; retrying.",
                _ => "Log temporarily unavailable; retrying."
            };
            warning?.Invoke(message);
            Diagnostic?.Invoke(new(condition, message));
        }
        var reader = new TailReader(position, Report, identityProvider ?? new HandleIdentityProvider());
        var delay = timer ?? new AsyncTimer();
        var failures = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using (var poll = reader.ReadAsync(path, cancellationToken).GetAsyncEnumerator(cancellationToken))
            {
                while (true)
                {
                    bool next;
                    try
                    {
                        next = await poll.MoveNextAsync().ConfigureAwait(false);
                        if (!next) failures = 0;
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        failures = Math.Min(failures + 1, 5);
                        Report(e is UnauthorizedAccessException ? FileTailCondition.AccessDenied : FileTailCondition.Unavailable);
                        break;
                    }
                    if (!next) break;
                    yield return poll.Current;
                }
            }
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
