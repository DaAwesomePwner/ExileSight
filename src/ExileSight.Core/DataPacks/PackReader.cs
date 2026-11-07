using System.Text.Json;
using ExileSight.Core.Features;

namespace ExileSight.Core.DataPacks;

/// <summary>An immutable versioned snapshot. Data returns a detached copy because wire models
/// contain mutable arrays/dictionaries. Keep that copy locally when building feature indexes.</summary>
public sealed class PackSnapshot<T> where T : class
{
    private readonly byte[] bytes;
    internal PackSnapshot(string packId, string version, string source, byte[] bytes)
    { PackId = packId; Version = version; Source = source; this.bytes = bytes; }
    public string PackId { get; }
    public string Version { get; }
    public string Source { get; }
    public T Data => Select(JsonSerializer.Deserialize(bytes, PackJson.Default.PackContent)!)!;
    internal static T? Select(PackContent content)
    {
        object? value = typeof(T) == typeof(PackContent) ? content : content.PackId switch
        {
            "poe1/guide" => content.Guide,
            "poe1/mapmods" => content.MapMods,
            "poe1/events" => content.Events,
            "poe1/leagues" => content.Leagues,
            _ => null
        };
        return value as T;
    }
}

/// <summary>Read-side publication of loader-validated packs. Never exposes shared mutable models.</summary>
public sealed class PackReader : IPackReader
{
    private sealed record Entry(string Version, string Source, string Hash, byte[] Bytes);
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    public event Action<PackChange>? Changed;
    public Action<Exception>? ReportFailure { get; init; }
    public PackSnapshot<T>? TryGet<T>(string packId) where T : class
    {
        Entry? entry;
        lock (gate) entries.TryGetValue(packId, out entry);
        if (entry is null) return null;
        var content = JsonSerializer.Deserialize(entry.Bytes, PackJson.Default.PackContent)!;
        return PackSnapshot<T>.Select(content) is null ? null : new(packId, entry.Version, entry.Source, entry.Bytes);
    }
    public void Publish(string packId, DataPack? pack, string source)
    {
        if (pack is not null && (pack.Manifest.PackId != packId || pack.Content.PackId != packId))
            throw new ArgumentException("Pack identity does not match publication ID", nameof(pack));
        var bytes = pack is null ? null : PackCodec.ContentBytes(pack.Content);
        var next = pack is null ? null : new Entry(pack.Manifest.PackVersion, source, PackCodec.Hash(bytes!), bytes!);
        bool changed;
        lock (gate)
        {
            entries.TryGetValue(packId, out var previous);
            changed = previous?.Version != next?.Version || previous?.Hash != next?.Hash;
            if (next is null) entries.Remove(packId);
            else entries[packId] = next;
        }
        if (!changed) return;
        var change = new PackChange(packId, next?.Version, next?.Source ?? "none");
        foreach (var handler in Changed?.GetInvocationList() ?? [])
        {
            try { ((Action<PackChange>)handler)(change); }
            catch (Exception error)
            {
                try { ReportFailure?.Invoke(error); }
                catch (Exception) { /* Diagnostics cannot interrupt publication. */ }
            }
        }
    }
}
