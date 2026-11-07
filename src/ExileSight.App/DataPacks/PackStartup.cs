using ExileSight.Core.DataPacks;
using ExileSight.Core.Diagnostics;
using ExileSight.Features.ItemInfo;

namespace ExileSight.App.DataPacks;

// Portable coordinator: IO delegates let tests exercise startup without WPF or network.
public sealed class PackStartup(
    Func<string, CancellationToken, Task<(DataPack? Pack, string Source)>> read,
    Func<string, CancellationToken, Task> refresh) : IDebugSection
{
    public static IReadOnlyList<string> PackIds { get; } = Array.AsReadOnly(new[] { "poe1/bases", "poe1/mods", "poe1/stats" });
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
            var result = await read(id, token).ConfigureAwait(false);
            packs.Add(result.Pack);
            rows.Add(new(id, $"{result.Pack?.Manifest.PackVersion ?? "unavailable"} ({result.Source})"));
        }
        var nextIdentity = string.Join("|", packs.Select(p => p is null ? "missing" :
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
        Volatile.Write(ref state, new(data, packs.All(p => p is not null) ? "Packs ready" : packs.Any(p => p is not null) ? "Packs unavailable (partial data)" : "Packs unavailable", rows.ToArray()));
    }
    public async Task RefreshAsync(CancellationToken token)
    {
        foreach (var id in PackIds) await refresh(id, token).ConfigureAwait(false);
        // PackLoader deliberately returns local fallback on failures; don't label this as a successful download.
        await LoadAsync($"Checked {DateTimeOffset.UtcNow:u}; local data retained if download failed", token).ConfigureAwait(false);
    }
}
