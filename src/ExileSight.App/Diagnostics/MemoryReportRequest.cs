using System.IO;

namespace ExileSight.App.Diagnostics;

// One-shot, own-directory diagnostic request. No polling or resource sampling while idle.
public static class MemoryReportRequest
{
    public const string FileName = "perf-memory.request";
    public static async Task WaitAsync(string directory, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new FileSystemWatcher(directory, FileName) { NotifyFilter = NotifyFilters.FileName };
        watcher.Created += (_, _) => ready.TrySetResult();
        watcher.Renamed += (_, _) => ready.TrySetResult();
        watcher.Error += (_, error) => ready.TrySetException(error.GetException());
        watcher.EnableRaisingEvents = true;
        // Close the race between the request arriving and installing the watcher.
        if (File.Exists(Path.Combine(directory, FileName))) return;
        await ready.Task.WaitAsync(token).ConfigureAwait(false);
    }
}
