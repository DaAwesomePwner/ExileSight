namespace ExileSight.Core.Input;

/// <summary>A timed-out native provider cannot stall the action worker or create unlimited readers.</summary>
internal sealed class BoundedClipboardRead<T>
{
    private readonly object sync = new();
    private Task<T>? pending;
    internal T Read(Func<CancellationToken, T> read, int timeoutMilliseconds = 500, Action? pump = null)
    {
        lock (sync)
        {
            if (pending is { IsCompleted: false }) throw new TimeoutException("Clipboard provider still busy");
            using var timeout = new CancellationTokenSource();
            var token = timeout.Token;
            pending = Task.Run(() => read(token));
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            while (!pending.IsCompleted)
            {
                pump?.Invoke();
                var remaining = timeoutMilliseconds - elapsed.ElapsedMilliseconds;
                if (remaining <= 0)
                {
                    timeout.Cancel();
                    throw new TimeoutException("Clipboard read timed out");
                }
                try { pending.Wait((int)Math.Min(5, remaining)); }
                catch (AggregateException) { break; } // GetAwaiter below preserves the provider exception type.
            }
            return pending.GetAwaiter().GetResult();
        }
    }
}
