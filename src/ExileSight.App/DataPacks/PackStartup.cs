using System.Diagnostics;
using ExileSight.Core.Input;
using System.Runtime;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Diagnostics;
using ExileSight.Features.ItemInfo;

namespace ExileSight.App.DataPacks;

// Portable coordinator: IO delegates let tests exercise startup without WPF or network.
// The hosted service is the single serial caller of LoadAsync/RefreshAsync.
public sealed class PackStartup(
    Func<string, CancellationToken, Task<(DataPack? Pack, string Source)>> read,
    Func<string, CancellationToken, Task> refresh, Action<string, Exception>? log = null) : IDebugSection
{
    public static IReadOnlyList<string> PackIds { get; } = Array.AsReadOnly(new[] { "poe1/bases", "poe1/mods", "poe1/stats",
        "poe1/guide", "poe1/mapmods", "poe1/events", "poe1/leagues" });
    private readonly PackPublisher publisher = new();
    private readonly Dictionary<string, (PackManifest? Manifest, string Source)> retained = [];
    public ExileSight.Core.Features.IPackReader Packs => publisher.Reader;
    private sealed record State(IItemInfoData? Data, string Status, KeyValuePair<string, string>[] Rows);
    private State state = new(null, "Packs loading", [new("Status", "Packs loading"), new("Last refresh", "Not checked")]);
    public IItemInfoData? Data => Volatile.Read(ref state).Data;
    private string? identity;
    public string? TooltipStatus => Status == "Packs ready" ? null : Status;
    public string Status => Volatile.Read(ref state).Status;
    public string Title => "PoE1 data packs";
    public IReadOnlyList<KeyValuePair<string, string>> Snapshot() => Volatile.Read(ref state).Rows;
    public void ReportFailure(string operation, Exception error)
    {
        var previous = Volatile.Read(ref state);
        Volatile.Write(ref state, previous with
        {
            Status = previous.Data is null ? "Packs unavailable" : previous.Status,
            Rows = previous.Rows.Where(row => row.Key != "Last error").Append(
                new KeyValuePair<string, string>("Last error", $"{operation}: {error.GetType().Name}")).ToArray()
        });
    }
    public double LastLoadReclaimMilliseconds { get; private set; }
    public bool LastLoadReclaimOnThreadPool { get; private set; }
    public long LastLoadCommittedBytes { get; private set; }
    // Start loading on the thread pool even when invoked from the hook/UI thread.
    // Reclamation is independently queued below after asynchronous reads finish.
    public Task LoadAsync(string refreshResult, CancellationToken token) =>
        Task.Run(() => LoadInBackgroundAsync(refreshResult, token), token);
    private async Task LoadInBackgroundAsync(string refreshResult, CancellationToken token)
    {
        var loadStart = Stopwatch.GetTimestamp();
        InputTiming.Record(InputStage.PackLoadStart, 0);
        var allocatedBefore = GC.GetTotalAllocatedBytes();
        try { await PrepareAsync(refreshResult, token).ConfigureAwait(false); }
        finally
        {
            // Shipping pack decoding creates hundreds of MB of temporary wire graphs/LOH
            // arrays. Aggressive collection requests decommit of empty segments here,
            // unlike Forced which can leave >100 MB committed. Never on an idle tick or
            // item check. Small loads (<32 MB allocated) do not warrant a blocking collection.
            LastLoadReclaimMilliseconds = 0;
            LastLoadReclaimOnThreadPool = false;
            LastLoadCommittedBytes = GC.GetGCMemoryInfo().TotalCommittedBytes;
            if (GC.GetTotalAllocatedBytes() - allocatedBefore >= 32_000_000)
            {
                await Task.Run(() =>
                {
                    // Explicit scheduling also covers a read delegate completing inline
                    // on a non-pool thread. No cancellation after the allocations occurred.
                    LastLoadReclaimOnThreadPool = Thread.CurrentThread.IsThreadPoolThread;
                    InputTiming.Record(InputStage.PackReclaimStart, 0);
                    var clock = Stopwatch.StartNew();
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                    LastLoadReclaimMilliseconds = clock.Elapsed.TotalMilliseconds;
                    InputTiming.Record(InputStage.PackReclaimEnd, (long)LastLoadReclaimMilliseconds);
                    LastLoadCommittedBytes = GC.GetGCMemoryInfo().TotalCommittedBytes;
                }).ConfigureAwait(false);
            }
            InputTiming.Record(InputStage.PackLoadEnd, (long)Stopwatch.GetElapsedTime(loadStart).TotalMilliseconds);
        }
    }
    private async Task PrepareAsync(string refreshResult, CancellationToken token)
    {
        var manifests = new List<PackManifest?>();
        var itemContent = new PackContent?[3];
        var rows = new List<KeyValuePair<string, string>>();
        var errors = new List<(string Id, Exception Error)>();
        foreach (var id in PackIds)
        {
            (PackManifest? Manifest, string Source) result;
            try
            {
                var loaded = await read(id, token).ConfigureAwait(false);
                publisher.Publish(id, loaded.Pack, loaded.Source);
                result = (loaded.Pack?.Manifest, loaded.Source);
                retained[id] = result;
                if (manifests.Count < 3) itemContent[manifests.Count] = loaded.Pack?.Content;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                log?.Invoke(id, error);
                errors.Add((id, error));
                result = retained.GetValueOrDefault(id, (null, "none"));
            }
            manifests.Add(result.Manifest);
            rows.Add(new(id, $"{result.Manifest?.PackVersion ?? "unavailable"} ({result.Source})"));
        }
        var nextIdentity = string.Join("|", manifests.Take(3).Select(p => p is null ? "missing" :
            p.PackVersion + ":" + p.ContentSha256));
        var data = Data;
        if (data is null || identity != nextIdentity)
        {
            // Compact once on the background pack-loading path, while validated DTOs are
            // already available. Do not deserialize three complete copies at first item use.
            PackContent? Content(int index) => itemContent[index] ??
                (manifests[index] is null ? null : Packs.TryGet<PackDocument>(PackIds[index])?.CreateCopy());
            data = new PackItemInfoData(Content(0), Content(1), Content(2));
        }
        token.ThrowIfCancellationRequested();
        identity = nextIdentity;
        rows.Add(new("Last refresh", refreshResult));
        rows.Add(new("Release availability", "Missing release assets (including Phase 3 HTTP 404s) remain throttled to at most one attempt per pack/tag per hour."));
        Volatile.Write(ref state, new(data, manifests.Take(3).All(p => p is not null) ? "Packs ready" : manifests.Take(3).Any(p => p is not null) ? "Packs unavailable (partial data)" : "Packs unavailable", rows.ToArray()));
        foreach (var (id, error) in errors) ReportFailure(id, error);
    }
    public async Task RefreshAsync(CancellationToken token)
    {
        var failures = new List<string>();
        foreach (var id in PackIds)
        {
            token.ThrowIfCancellationRequested();
            try { await refresh(id, token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                failures.Add($"{id}: {error.GetType().Name}");
                log?.Invoke(id, error);
            }
        }
        // PackLoader deliberately returns local fallback on failures; don't label this as a successful download.
        await LoadAsync($"Checked {DateTimeOffset.UtcNow:u}; local data retained if download failed" +
            (failures.Count == 0 ? "" : "; " + string.Join(", ", failures)), token).ConfigureAwait(false);
    }
}
