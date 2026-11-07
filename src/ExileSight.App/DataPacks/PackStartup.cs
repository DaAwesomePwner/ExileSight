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
    public async Task LoadAsync(string refreshResult, CancellationToken token)
    {
        var manifests = new List<PackManifest?>();
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
            // Capture this publication's immutable snapshots, not the mutable publisher.
            // Item data is not needed by a tray-idle app; construct it on first item use.
            data = new DeferredItemData(Packs.TryGet<PackDocument>("poe1/bases"),
                Packs.TryGet<PackDocument>("poe1/mods"), Packs.TryGet<PackDocument>("poe1/stats"));
        }
        token.ThrowIfCancellationRequested();
        identity = nextIdentity;
        rows.Add(new("Last refresh", refreshResult));
        rows.Add(new("Release availability", "Missing release assets (including Phase 3 HTTP 404s) remain throttled to at most one attempt per pack/tag per hour."));
        Volatile.Write(ref state, new(data, manifests.Take(3).All(p => p is not null) ? "Packs ready" : manifests.Take(3).Any(p => p is not null) ? "Packs unavailable (partial data)" : "Packs unavailable", rows.ToArray()));
        foreach (var (id, error) in errors) ReportFailure(id, error);
    }
    private sealed class DeferredItemData(PackSnapshot<PackDocument>? bases,
        PackSnapshot<PackDocument>? mods, PackSnapshot<PackDocument>? stats) : IItemInfoData
    {
        private readonly Lazy<PackItemInfoData> data = new(() => new(bases?.CreateCopy(), mods?.CreateCopy(), stats?.CreateCopy()));
        public BaseEntry? FindBase(ExileSight.Core.Items.ParsedItem item) => data.Value.FindBase(item);
        public ModEntry? FindMod(ExileSight.Core.Items.ParsedItem item, IReadOnlyList<ExileSight.Core.Items.ModLine> group) => data.Value.FindMod(item, group);
        public StatMatch[] MatchLine(ExileSight.Core.Items.ModLine line) => data.Value.MatchLine(line);
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
