using System.Net;
using System.Text.Json;

namespace ExileSight.Core.DataPacks.Loading;

public enum PackAvailability { Unavailable, Available }
public sealed record PackResult(DataPack? Pack)
{
    public PackAvailability Availability => Pack is null ? PackAvailability.Unavailable : PackAvailability.Available;
}

/// <summary>Startup uses ReadLocalAsync (no network). Refresh is explicitly scheduled by the caller.</summary>
public sealed class PackLoader(string cacheDirectory, GitHubTransport transport, string? fallbackDirectory = null, TimeProvider? timeProvider = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> checkedAt = new();
    public static void ValidateSegment(string value)
    {
        if (string.IsNullOrEmpty(value) || value.All(c => c == '.') || value.Contains("..", StringComparison.Ordinal) ||
            !value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            throw new ArgumentException("Release tags and asset names must contain only ASCII letters, digits, dots, underscores or hyphens, without dot-only segments or '..'.");
    }
    public static string AssetName(string packId)
    {
        var asset = packId switch
        {
            "poe1/guide" => "poe1-guide", "poe1/mapmods" => "poe1-mapmods",
            "poe1/events" => "poe1-events", "poe1/leagues" => "poe1-leagues",
            "poe1/stats" => "poe1-stats",
            "poe1/bases" => "poe1-bases", "poe1/mods" => "poe1-mods",
            "poe2/bases" => "poe2-bases", "poe2/mods" => "poe2-mods",
            _ => throw new ArgumentException("Unknown pack ID.", nameof(packId))
        };
        ValidateSegment(asset);
        return asset;
    }
    public async Task<PackResult> ReadLocalAsync(string packId, CancellationToken cancellationToken = default)
    {
        var asset = AssetName(packId);
        var cached = await ReadAsync(Path.Combine(cacheDirectory, asset + ".pack.json"), packId, cancellationToken);
        if (cached is null && fallbackDirectory is not null)
            cached = await ReadAsync(Path.Combine(fallbackDirectory, asset + ".pack.json"), packId, cancellationToken);
        return new(cached?.Pack);
    }
    public async Task<PackResult> RefreshAsync(string packId, string releaseTag, CancellationToken cancellationToken = default)
    {
        ValidateSegment(releaseTag);
        var asset = AssetName(packId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var key = packId + ":" + releaseTag;
            var path = Path.Combine(cacheDirectory, asset + ".pack.json");
            var cached = (await ReadAsync(path, packId, cancellationToken))?.Cache;
            var matching = cached?.ReleaseTag == releaseTag ? cached : null;
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
            var last = checkedAt.GetValueOrDefault(key);
            // Ignore future persisted checks so they cannot override the in-memory failure throttle.
            if (matching is not null && matching.CheckedAt <= now && matching.CheckedAt > last) last = matching.CheckedAt;
            if (last <= now && now - last < TimeSpan.FromHours(1)) return await ReadLocalAsync(packId, cancellationToken);
            checkedAt[key] = now;
            var root = "https://github.com/DaAwesomePwner/ExileSight/releases/download/" + releaseTag + "/";
            var uri = new Uri(root + asset + ".manifest.json");
            using var initial = await transport.GetAsync(uri, matching?.ETag, cancellationToken);
            if (initial.StatusCode == HttpStatusCode.NotModified)
            {
                // Re-read: the cache could have disappeared or become corrupt during the request.
                var current = await ReadAsync(path, packId, cancellationToken);
                if (matching is not null && current?.Cache.ReleaseTag == releaseTag && current.Cache.ETag == matching.ETag)
                {
                    await WriteAsync(path, current.Cache with { CheckedAt = now }, cancellationToken);
                    checkedAt.Remove(key);
                    return new(current.Pack);
                }
            }
            using var retry = initial.StatusCode == HttpStatusCode.NotModified
                ? await transport.GetAsync(uri, null, cancellationToken) : null;
            var response = retry ?? initial;
            response.EnsureSuccessStatusCode();
            var manifest = JsonSerializer.Deserialize(await transport.ReadBytesAsync(response, cancellationToken), PackJson.Default.PackManifest);
            if (manifest is null) return await ReadLocalAsync(packId, cancellationToken);
            using var contentResponse = await transport.GetAsync(new Uri(root + asset + ".json"), null, cancellationToken);
            contentResponse.EnsureSuccessStatusCode();
            var bytes = await transport.ReadBytesAsync(contentResponse, cancellationToken);
            var pack = PackCodec.Validate(manifest, bytes, packId);
            if (pack is null) return await ReadLocalAsync(packId, cancellationToken);
            await WriteAsync(path, new(manifest, bytes, releaseTag, now, response.Headers.ETag?.ToString()), cancellationToken);
            checkedAt.Remove(key); // Successful freshness belongs to the validated envelope, not a missing/replaced file.
            return new(pack);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or JsonException or UnauthorizedAccessException or OperationCanceledException or ArgumentException)
        {
            return await ReadLocalAsync(packId, CancellationToken.None);
        }
        finally { gate.Release(); }
    }
    private sealed record ValidatedCache(CachedPack Cache, DataPack Pack);
    private static async Task<ValidatedCache?> ReadAsync(string path, string packId, CancellationToken token)
    {
        try
        {
            var cached = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(path, token), PackJson.Default.CachedPack);
            return cached is not null && PackCodec.Validate(cached.Manifest, cached.ContentBytes, packId) is { } pack
                ? new(cached, pack) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    private static async Task WriteAsync(string path, CachedPack cached, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(cached, PackJson.Default.CachedPack), token);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
