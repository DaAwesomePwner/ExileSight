using System.Diagnostics;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.Platform.Windows.Window;

public sealed class OwnProcessStats : IProcessStats, IDisposable
{
    private readonly object gate = new();
    private readonly Process process = Process.GetCurrentProcess();
    private long lastTime = Stopwatch.GetTimestamp();
    private TimeSpan lastCpu;
    private bool sampled;

    public ProcessStats Sample()
    {
        lock (gate)
        {
            process.Refresh();
            long now = Stopwatch.GetTimestamp();
            TimeSpan cpu = process.TotalProcessorTime;
            double seconds = Stopwatch.GetElapsedTime(lastTime, now).TotalSeconds;
            double percent = sampled && seconds > 0
                ? (cpu - lastCpu).TotalSeconds / seconds / Environment.ProcessorCount * 100 : 0;
            lastTime = now;
            lastCpu = cpu;
            sampled = true;
            return new(process.WorkingSet64, process.PrivateMemorySize64, Math.Clamp(percent, 0, 100));
        }
    }

    public void Dispose() { lock (gate) process.Dispose(); }
}
