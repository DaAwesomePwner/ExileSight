using System.Net;
using System.Text.Json;

namespace ExileSight.Core.DataPacks.Loading;

public enum PackAvailability { Unavailable, Available }
public sealed record PackResult(DataPack? Pack)
{
    public PackAvailability Availability => Pack is null ? PackAvailability.Unavailable : PackAvailability.Available;
}

/// <summary>ReadLocalAsync is the startup path (no network). RefreshAsync is explicitly scheduled by the caller.</summary>
public sealed class PackLoader(string cacheDirectory, GitHubTransport transport, string? fallbackDirectory = null, TimeProvider? timeProvider = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> checkedAt = new();
    private readonly Dictionary<string, string?> etags = new();

    public static string AssetName(string packId) => packId switch
    {
        "poe1/bases" => "poe1-bases", "poe1/mods" => "poe1-mods",
        "poe2/bases" => "poe2-bases", "poe2/mods" => "poe2-mods",
        _ => throw new ArgumentException("Unknown pack ID.", nameof(packId))
    };

    public async Task<PackResult> ReadLocalAsync(string packId, CancellationToken cancellationToken = default)
    {
        var asset = AssetName(packId);
        var pack = await ReadAsync(Path.Combine(cacheDirectory, asset + ".pack.json"), packId, cancellationToken);
        if (pack is null && fallbackDirectory is not null)
            pack = await ReadAsync(Path.Combine(fallbackDirectory, asset + ".pack.json"), packId, cancellationToken);
        return new(pack);
    }

    public async Task<PackResult> RefreshAsync(string packId, string releaseTag, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var asset = AssetName(packId);
            var key = packId + ":" + releaseTag;
            var path = Path.Combine(cacheDirectory, asset + ".pack.json");
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
            var last = checkedAt.GetValueOrDefault(key);
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) > last.UtcDateTime)
                last = File.GetLastWriteTimeUtc(path);
            if (now - last < TimeSpan.FromHours(1)) return await ReadLocalAsync(packId, cancellationToken);
            checkedAt[key] = now; // Failed checks are throttled too.
            var root = "https://github.com/DaAwesomePwner/ExileSight/releases/download/" + Uri.EscapeDataString(releaseTag) + "/";
            using var manifestResponse = await transport.GetAsync(new Uri(root + asset + ".manifest.json"),
                etags.GetValueOrDefault(key), cancellationToken);
            if (manifestResponse.StatusCode == HttpStatusCode.NotModified) return await ReadLocalAsync(packId, cancellationToken);
            manifestResponse.EnsureSuccessStatusCode();
            var manifest = JsonSerializer.Deserialize(await manifestResponse.Content.ReadAsByteArrayAsync(cancellationToken), PackJson.Default.PackManifest);
            if (manifest is null) return await ReadLocalAsync(packId, cancellationToken);
            using var contentResponse = await transport.GetAsync(new Uri(root + asset + ".json"), null, cancellationToken);
            contentResponse.EnsureSuccessStatusCode();
            var bytes = await contentResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            if (PackCodec.Hash(bytes) != manifest.ContentSha256) return await ReadLocalAsync(packId, cancellationToken);
            var content = JsonSerializer.Deserialize(bytes, PackJson.Default.PackContent);
            if (content is null) return await ReadLocalAsync(packId, cancellationToken);
            var pack = new DataPack(manifest, content);
            if (!PackCodec.IsValid(pack, packId)) return await ReadLocalAsync(packId, cancellationToken);
            await WriteAtomicAsync(path, JsonSerializer.SerializeToUtf8Bytes(pack, PackJson.Default.DataPack), cancellationToken);
            etags[key] = manifestResponse.Headers.ETag?.ToString();
            return new(pack);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or JsonException or UnauthorizedAccessException or OperationCanceledException or ArgumentException)
        {
            return await ReadLocalAsync(packId, CancellationToken.None);
        }
        finally { gate.Release(); }
    }

    private static async Task<DataPack?> ReadAsync(string path, string packId, CancellationToken token)
    {
        try
        {
            var pack = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(path, token), PackJson.Default.DataPack);
            return pack is not null && PackCodec.IsValid(pack, packId) ? pack : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    private static async Task WriteAtomicAsync(string path, byte[] bytes, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, token);
            File.Move(temporary, path, true); // One envelope: manifest and content never become mixed generations.
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
