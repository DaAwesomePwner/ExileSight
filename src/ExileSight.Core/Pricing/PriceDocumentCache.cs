using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Pricing;

internal sealed record CachedPriceDocument(string? Json, DateTimeOffset? UpdatedAt, bool IsStale, string? Diagnostic);

/// <summary>Persist attempts before HTTP. Readers retain the last published copy while a writer refreshes.</summary>
internal sealed class PriceDocumentCache(IAppPaths paths, IClock clock, PriceTransport transport, IPriceCacheReader? reader = null)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();
    private readonly ConcurrentDictionary<string, PriceCache> memory = new();
    private readonly IPriceCacheReader reader = reader ?? new PriceCacheReader();
    private readonly RootedFiles files = new(paths.RootDirectory);
    public async Task<CachedPriceDocument> GetAsync(Uri uri, Action<string> validate, TimeSpan refreshInterval,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var interval = refreshInterval < TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : refreshInterval;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri.ToUpperInvariant())));
        var path = files.Check(Path.Combine(paths.RootDirectory, "cache", "poe1", "poeninja", key + ".json"));
        // A published, current copy needs neither filesystem reads nor the refresh lock.
        if (memory.TryGetValue(path, out var current) && IsWaiting(current, interval) &&
            current.LastAttempt <= clock.UtcNow && !(current.UpdatedAt > clock.UtcNow))
            return Result(current, interval, current.Error);
        var gate = Locks.GetOrAdd(path, _ => new(1, 1));
        if (!gate.Wait(0)) return Result(ReadAvailable(path), interval, "Price refresh in progress.");
        PriceCache cache = new();
        try
        {
            cache = ReadAvailable(path);
            Directory.CreateDirectory(files.Check(Path.GetDirectoryName(path)!));
            using var lease = new FileStream(files.Check(path + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            // Repair decisions are based exclusively on a fresh read while holding both locks.
            cache = Read(path, allowRepair: true);
            var now = clock.UtcNow;
            if (cache.LastAttempt > now || cache.UpdatedAt > now || cache.NeedsRepair)
            {
                cache = cache with
                {
                    LastAttempt = cache.LastAttempt > now ? now : cache.LastAttempt,
                    UpdatedAt = cache.UpdatedAt > now ? now : cache.UpdatedAt,
                    NeedsRepair = false
                };
                Write(path, cache);
            }
            memory[path] = cache;
            if (IsWaiting(cache, interval)) return Result(cache, interval, cache.Error);
            cancellationToken.ThrowIfCancellationRequested();
            cache = cache with { LastAttempt = now, Error = "Price refresh did not complete." };
            Write(path, cache); // Failure here must prevent HTTP.
            memory[path] = cache;
            try
            {
                var response = await transport.GetAsync(uri, cache.ETag, cache.LastModified, cancellationToken).ConfigureAwait(false);
                var json = response.Json ?? cache.Json ?? throw new JsonException("304 without cached overview.");
                validate(json);
                cache = cache with
                {
                    Json = json, UpdatedAt = clock.UtcNow, ETag = response.ETag,
                    LastModified = response.LastModified, Error = null
                };
                Write(path, cache);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception e) when (e is HttpRequestException or IOException or JsonException or
                OperationCanceledException or OverflowException or UriFormatException)
            {
                cache = cache with { Error = $"Price refresh unavailable ({e.GetType().Name}); cached prices may be stale." };
                Write(path, cache);
            }
            memory[path] = cache;
            return Result(cache, interval, cache.Error);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Result(cache, interval, "Price cache unavailable; serving the existing copy.");
        }
        finally { gate.Release(); }
    }
    private bool IsWaiting(PriceCache cache, TimeSpan interval) =>
        cache.LastAttempt is { } attempted && clock.UtcNow - attempted <
            (cache.Error is null ? interval : TimeSpan.FromHours(1));

    private CachedPriceDocument Result(PriceCache cache, TimeSpan interval, string? diagnostic)
    {
        var updated = cache.UpdatedAt > clock.UtcNow ? clock.UtcNow : cache.UpdatedAt;
        var stale = cache.Json is null || updated is null || clock.UtcNow - updated >= interval || diagnostic is not null;
        return new(cache.Json, updated, stale, diagnostic);
    }
    private PriceCache ReadAvailable(string path)
    {
        if (memory.TryGetValue(path, out var cached)) return cached;
        try
        {
            var result = Read(path, allowRepair: false);
            // An unlocked read can finish after the writer publishes. Never replace that copy.
            return memory.GetOrAdd(path, result);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return memory.TryGetValue(path, out var latest) ? latest :
                new() { Error = "Price cache unavailable." };
        }
    }
    private PriceCache Read(string path, bool allowRepair)
    {
        path = files.Check(path);
        if (!File.Exists(path)) return new();
        try
        {
            return JsonSerializer.Deserialize(reader.Read(path), PriceJsonContext.Default.PriceCache) ?? throw new JsonException();
        }
        catch (Exception e) when (allowRepair && e is (JsonException or IOException or UnauthorizedAccessException))
        {
            var modified = reader.LastWriteTime(path);
            return new()
            {
                LastAttempt = modified > clock.UtcNow ? clock.UtcNow : modified,
                Error = "Invalid price cache; retry after one hour.", NeedsRepair = true
            };
        }
    }
    private void Write(string path, PriceCache cache) =>
        files.Write(path, JsonSerializer.SerializeToUtf8Bytes(cache, PriceJsonContext.Default.PriceCache), false);
}
