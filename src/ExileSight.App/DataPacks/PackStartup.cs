using ExileSight.Core.DataPacks;
using ExileSight.Core.Diagnostics;
using ExileSight.Features.ItemInfo;

namespace ExileSight.App.DataPacks;

// Portable coordinator: IO delegates let tests exercise startup without WPF or network.
public sealed class PackStartup(
    Func<string, CancellationToken, Task<(DataPack? Pack, string Source)>> read,
    Func<string, CancellationToken, Task> refresh, Action<string, Exception>? log = null) : IDebugSection
{
    public static IReadOnlyList<string> PackIds { get; } = Array.AsReadOnly(new[] { "poe1/bases", "poe1/mods", "poe1/stats",
        "poe1/guide", "poe1/mapmods", "poe1/events", "poe1/leagues" });
    private readonly PackReader reader = new() { ReportFailure = error => log?.Invoke("Pack subscriber", error) };
    public ExileSight.Core.Features.IPackReader Packs => reader;
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
        var packs = new List<DataPack?>();
        var rows = new List<KeyValuePair<string, string>>();
        foreach (var id in PackIds)
        {
            (DataPack? Pack, string Source) result;
            try { result = await read(id, token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                log?.Invoke(id, error);
                result = (null, "none");
            }
            reader.Publish(id, result.Pack, result.Source);
            packs.Add(result.Pack);
            rows.Add(new(id, $"{result.Pack?.Manifest.PackVersion ?? "unavailable"} ({result.Source})"));
        }
        var nextIdentity = string.Join("|", packs.Take(3).Select(p => p is null ? "missing" :
            p.Manifest.PackVersion + ":" + p.Manifest.ContentSha256));
        var data = Data;
        if (data is null || identity != nextIdentity)
        {
            var prepared = new PackItemInfoData(packs[0]?.Content, packs[1]?.Content, packs[2]?.Content);
            await prepared.PrepareAsync(token).ConfigureAwait(false);
            data = prepared;
        }
        token.ThrowIfCancellationRequested();
        identity = nextIdentity;
        rows.Add(new("Last refresh", refreshResult));
        Volatile.Write(ref state, new(data, packs.Take(3).All(p => p is not null) ? "Packs ready" : packs.Take(3).Any(p => p is not null) ? "Packs unavailable (partial data)" : "Packs unavailable", rows.ToArray()));
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
