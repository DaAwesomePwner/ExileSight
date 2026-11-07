namespace ExileSight.Core.Input;

/// <summary>A timed-out native provider cannot stall the action worker or create unlimited readers.</summary>
internal sealed class BoundedClipboardRead<T>
{
    private readonly object sync = new();
    private Task<T>? pending;
    internal T Read(Func<CancellationToken, T> read, int timeoutMilliseconds = 500)
    {
        lock (sync)
        {
            if (pending is { IsCompleted: false }) throw new TimeoutException("Clipboard provider still busy");
            using var timeout = new CancellationTokenSource();
            var token = timeout.Token;
            pending = Task.Run(() => read(token));
            if (!pending.Wait(timeoutMilliseconds))
            {
                timeout.Cancel();
                throw new TimeoutException("Clipboard preservation timed out");
            }
            return pending.GetAwaiter().GetResult();
        }
    }
}
