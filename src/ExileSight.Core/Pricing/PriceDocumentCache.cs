using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Pricing;

internal sealed record CachedPriceDocument(string? Json, DateTimeOffset? UpdatedAt, bool IsStale, string? Diagnostic);

/// <summary>Persist attempts before HTTP, including failed/cancelled requests. Never refresh inside one hour.</summary>
internal sealed class PriceDocumentCache(IAppPaths paths, IClock clock, PriceTransport transport)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();
    private readonly RootedFiles files = new(paths.RootDirectory);
    public async Task<CachedPriceDocument> GetAsync(Uri uri, Action<string> validate, TimeSpan refreshInterval,
        CancellationToken cancellationToken = default)
    {
        var interval = refreshInterval < TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : refreshInterval;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        var path = files.Check(Path.Combine(paths.RootDirectory, "cache", "poe1", "poeninja", key + ".json"));
        var gate = Locks.GetOrAdd(path, _ => new(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        PriceCache cache = new();
        try
        {
            Directory.CreateDirectory(files.Check(Path.GetDirectoryName(path)!));
            // Prevent independent app instances from racing an attempt timestamp.
            using var lease = new FileStream(files.Check(path + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            cache = Read(path);
            var now = clock.UtcNow;
            if (cache.LastAttempt is { } attempted && now - attempted < interval)
                return Result(cache, interval, cache.Error);
            cancellationToken.ThrowIfCancellationRequested();
            cache.LastAttempt = now;
            cache.Error = "Price refresh did not complete.";
            Write(path, cache); // Failure here must prevent HTTP.
            try
            {
                var response = await transport.GetAsync(uri, cache.ETag, cache.LastModified, cancellationToken).ConfigureAwait(false);
                var json = response.Json ?? cache.Json ?? throw new JsonException("304 without cached overview.");
                validate(json);
                cache.Json = json;
                cache.UpdatedAt = clock.UtcNow;
                cache.ETag = response.ETag;
                cache.LastModified = response.LastModified;
                cache.Error = null;
                Write(path, cache);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception e) when (e is HttpRequestException or IOException or JsonException or OperationCanceledException)
            {
                cache.Error = "Price refresh unavailable; cached prices may be stale.";
                Write(path, cache);
            }
            return Result(cache, interval, cache.Error);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Result(cache, interval, "Price cache unavailable; no further request was made.");
        }
        finally { gate.Release(); }
    }
    private CachedPriceDocument Result(PriceCache cache, TimeSpan interval, string? diagnostic)
    {
        var stale = cache.Json is null || cache.UpdatedAt is null || clock.UtcNow - cache.UpdatedAt >= interval || diagnostic is not null;
        return new(cache.Json, cache.UpdatedAt, stale, diagnostic);
    }
    private PriceCache Read(string path)
    {
        if (!File.Exists(files.Check(path))) return new();
        if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new IOException("Price cache too large.");
        // A malformed timestamp record fails closed instead of bypassing the refresh limit.
        try { return JsonSerializer.Deserialize(File.ReadAllBytes(path), PriceJsonContext.Default.PriceCache) ?? throw new JsonException(); }
        catch (JsonException e) { throw new IOException("Invalid price cache.", e); }
    }
    private void Write(string path, PriceCache cache) =>
        files.Write(path, JsonSerializer.SerializeToUtf8Bytes(cache, PriceJsonContext.Default.PriceCache), false);
}
