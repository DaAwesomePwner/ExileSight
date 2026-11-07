using System.IO;
using System.Text.Json;
using ExileSight.Core.DataPacks.Loading;

namespace ExileSight.App.DataPacks;

/// <summary>Persist before networking. An interrupted attempt still consumes the hourly allowance.</summary>
public sealed class PackRefreshThrottle(string directory, TimeProvider? timeProvider = null)
{
    public sealed record Attempt(DateTimeOffset LastAttempt, string Result);
    private readonly SemaphoreSlim gate = new(1, 1);
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
            if (File.Exists(path))
            {
                // Fail closed if an existing throttle cannot be read; never turn corruption into a request loop.
                var previous = JsonSerializer.Deserialize<Attempt>(await File.ReadAllTextAsync(path, token).ConfigureAwait(false))
                    ?? throw new IOException("Invalid refresh attempt record");
                if (now - previous.LastAttempt < TimeSpan.FromHours(1)) return previous.Result;
            }
            await SaveAsync(path, new(now, "Attempt started; outcome unknown"), token).ConfigureAwait(false);
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
