using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExileSight.Core.Settings;
namespace ExileSight.Core.Pricing;

public sealed record PriceCacheSnapshot(bool Available, DateTimeOffset? UpdatedAt, string? Diagnostic);

/// <summary>Read-only accessor for the persisted PriceDocumentCache format. Never refreshes,
/// repairs or writes. Timestamp/size detection avoids reparsing an unchanged file.
/// Call on a worker thread; instances serialize access and retain only the last selected file.</summary>
public sealed class PriceCacheSnapshotReader(string rootDirectory)
{
    private readonly object sync = new();
    private readonly RootedFiles files = new(rootDirectory);
    private (string Path, DateTime Stamp, long Size)? previous;
    private PriceCacheSnapshot? snapshot;

    public PriceCacheSnapshot Read(string league, string category)
    {
        lock (sync)
        {
            try
            {
                var uri = PriceEndpoints.Overview(league, category);
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri.ToUpperInvariant())));
                var path = files.Check(Path.Combine(rootDirectory, "cache", "poe1", "poeninja", key + ".json"));
                var file = new FileInfo(path);
                var signature = (path, file.Exists ? file.LastWriteTimeUtc : default, file.Exists ? file.Length : -1);
                if (previous == signature && snapshot is not null) return snapshot;
                PriceCacheSnapshot next;
                if (!file.Exists) next = new(false, null, "No saved prices for this league.");
                else
                {
                    var cache = JsonSerializer.Deserialize(new PriceCacheReader().Read(path), PriceJsonContext.Default.PriceCache)
                        ?? throw new JsonException("Invalid price cache.");
                    if (cache.Json is not null) PriceParser.Parse(cache.Json, category);
                    next = new(cache.Json is not null, cache.UpdatedAt, cache.Error);
                }
                previous = signature;
                return snapshot = next;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or OverflowException)
            {
                previous = null; // Retry transient failures next time, without altering the file.
                return new(false, null, "Price cache unavailable.");
            }
        }
    }
}
