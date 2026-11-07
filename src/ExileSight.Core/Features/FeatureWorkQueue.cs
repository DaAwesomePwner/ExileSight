using System.Collections.Concurrent;

namespace ExileSight.Core.Features;

/// <summary>Serial token-free feature work, isolated from the action worker and UI dismissal.
/// Handlers must marshal their own view changes to the UI dispatcher.</summary>
public sealed class FeatureWorkQueue
{
    private readonly ConcurrentQueue<Action> work = new();
    private int running;
    public Action<string> ReportFailure { get; set; } = type => System.Diagnostics.Trace.TraceWarning("Feature handler failed: {0}", type);
    public void Post(Action action)
    {
        work.Enqueue(action);
        Start();
    }
    private void Start()
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) == 0)
            ThreadPool.QueueUserWorkItem(_ => Drain());
    }
    private void Drain()
    {
        try
        {
            while (work.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception error)
                {
                    try { ReportFailure(error.GetType().Name); }
                    catch (Exception) { /* Diagnostics cannot stop later feature work. */ }
                }
            }
        }
        finally
        {
            Volatile.Write(ref running, 0);
            if (!work.IsEmpty) Start();
        }
    }
}
