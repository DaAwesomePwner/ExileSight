using System.IO;
using System.Text.Json;
using ExileSight.Core.DataPacks.Loading;

namespace ExileSight.App.DataPacks;

/// <summary>Persist before networking. An interrupted attempt still consumes the hourly allowance.</summary>
public sealed class PackRefreshThrottle(string directory, TimeProvider? timeProvider = null)
{
    public sealed record Attempt(DateTimeOffset LastAttempt, string Result);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, Attempt> recovered = [];
    public async Task<string> RunAsync(string packId, string tag, Func<bool> setupComplete,
        Func<CancellationToken, Task<string>> refresh, CancellationToken token)
    {
        if (!setupComplete()) return "Waiting for first-run setup";
        PackLoader.ValidateSegment(tag);
        var path = Path.Combine(directory, PackLoader.AssetName(packId) + "." + tag + ".refresh.json");
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!setupComplete()) return "Waiting for first-run setup";
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
            var previous = await ReadAttemptAsync(path, now, token).ConfigureAwait(false);
            if (previous is not null && now - previous.LastAttempt < TimeSpan.FromHours(1)) return previous.Result;
            await SaveAsync(path, new(now, "Attempt started; outcome unknown"), token).ConfigureAwait(false);
            recovered.Remove(path);
            string result;
            try { result = await refresh(token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                await SaveAsync(path, new(now, "Failed: " + error.GetType().Name), token).ConfigureAwait(false);
                throw;
            }
            await SaveAsync(path, new(now, result), token).ConfigureAwait(false);
            return result;
        }
        finally { gate.Release(); }
    }
    private async Task<Attempt?> ReadAttemptAsync(string path, DateTimeOffset now, CancellationToken token)
    {
        if (recovered.TryGetValue(path, out var fallback)) return fallback;
        Attempt previous;
        try
        {
            previous = JsonSerializer.Deserialize<Attempt>(await File.ReadAllTextAsync(path, token).ConfigureAwait(false))
                ?? throw new JsonException("Null refresh attempt record");
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return null; }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            DateTimeOffset modified;
            try { modified = File.GetLastWriteTimeUtc(path); }
            catch (Exception metadataError) when (metadataError is IOException or UnauthorizedAccessException) { modified = now; }
            // Keep a stable recovery deadline even if the file timestamp is in the future or inaccessible.
            return recovered[path] = new(modified > now ? now : modified,
                "Invalid refresh record; retry after one hour");
        }
        if (previous.LastAttempt > now)
        {
            previous = previous with { LastAttempt = now };
            // Persist clock correction so subsequent calls/restarts do not move the deadline forward.
            recovered[path] = previous;
            await SaveAsync(path, previous, token).ConfigureAwait(false);
            recovered.Remove(path);
        }
        return previous;
    }
    private static async Task SaveAsync(string path, Attempt attempt, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(attempt), token).ConfigureAwait(false);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
