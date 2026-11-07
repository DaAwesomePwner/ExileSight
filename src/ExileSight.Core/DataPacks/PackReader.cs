using System.Text.Json;
using ExileSight.Core.Features;

namespace ExileSight.Core.DataPacks;

/// <summary>Immutable metadata and shared immutable data, parsed lazily once per published version.</summary>
public sealed class PackSnapshot<T> where T : class
{
    private readonly byte[] bytes;
    private readonly Lazy<object> data;
    internal PackSnapshot(string packId, string version, string source, byte[] bytes, Lazy<object> data)
    { PackId = packId; Version = version; Source = source; this.bytes = bytes; this.data = data; }
    public string PackId { get; }
    public string Version { get; }
    public string Source { get; }
    public T Data => (T)data.Value;
    /// <summary>Explicit O(pack size) deserialization for callers that need mutable wire data.
    /// This does not replace or mutate the shared, lazily parsed read model.</summary>
    public PackContent CreateCopy() => JsonSerializer.Deserialize(bytes, PackJson.Default.PackContent)!;
}

/// <summary>App-owned publication capability, separate from the reader given to features.
/// Call Publish from one serial publisher; notifications preserve that publication order.</summary>
public sealed class PackPublisher
{
    private readonly PackReader reader = new();
    public IPackReader Reader => reader;
    internal int ParseCount => reader.ParseCount;
    public void Publish(string packId, DataPack? pack, string source) => reader.Publish(packId, pack, source);
}

internal sealed class PackReader : IPackReader
{
    private sealed record Entry(string Version, string Source, string Hash, byte[] Bytes, Lazy<object> Data);
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private int parseCount;
    internal int ParseCount => Volatile.Read(ref parseCount);
    public event Action<PackChange>? Changed;
    public PackSnapshot<T>? TryGet<T>(string packId) where T : class
    {
        if (typeof(T) != PackViews.TypeFor(packId)) return null;
        lock (gate)
            return entries.TryGetValue(packId, out var e) ? new(packId, e.Version, e.Source, e.Bytes, e.Data) : null;
    }
    internal void Publish(string packId, DataPack? pack, string source)
    {
        if (pack is not null && (pack.Manifest.PackId != packId || pack.Content.PackId != packId))
            throw new ArgumentException("Pack identity does not match publication ID", nameof(pack));
        var bytes = pack is null ? null : PackCodec.ContentBytes(pack.Content);
        var hash = bytes is null ? null : PackCodec.Hash(bytes);
        bool changed;
        lock (gate)
        {
            entries.TryGetValue(packId, out var previous);
            changed = previous?.Version != pack?.Manifest.PackVersion || previous?.Hash != hash;
            if (pack is null) entries.Remove(packId);
            else if (!changed) entries[packId] = previous! with { Source = source };
            else entries[packId] = new(pack.Manifest.PackVersion, source, hash!, bytes!, new(() =>
            {
                Interlocked.Increment(ref parseCount);
                return PackViews.Parse(bytes!, packId);
            }, LazyThreadSafetyMode.ExecutionAndPublication));
        }
        if (!changed) return;
        var change = new PackChange(packId, pack?.Manifest.PackVersion, pack is null ? "none" : source);
        foreach (var handler in Changed?.GetInvocationList() ?? [])
        {
            try { ((Action<PackChange>)handler)(change); }
            catch (Exception) { /* Only host adapters subscribe here; feature failures are diagnosed by the host. */ }
        }
    }
}
