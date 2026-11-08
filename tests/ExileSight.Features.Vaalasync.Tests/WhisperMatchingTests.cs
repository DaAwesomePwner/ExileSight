using ExileSight.Core.Logs;
using ExileSight.Features.Vaalasync;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class WhisperMatchingTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "vaalasync-whisper-" + Guid.NewGuid());
    [Theory]
    [InlineData("Doom Grip", "Iron Ring", "Doom Grip")]
    [InlineData("Doom Grip", "Iron Ring", "Iron Ring")]
    [InlineData("Doom Grip", "Iron Ring", "Doom Grip Iron Ring")]
    [InlineData("Headhunter", "Leather Belt", "Headhunter Leather Belt")]
    public void MatchesItemNameBaseOrBothWithoutPersistingWhisperIdentity(string name, string baseType, string item)
    {
        var store = new HistoryStore(directory);
        store.Observe(name, baseType, 85, "Standard", new("~b/o", 2, "divine"), DateTimeOffset.UtcNow);
        store.Evidence(new("SECRET", null, "@From", "SECRET", "SECRET", item, "2 divine", "Standard", "SECRET"), DateTimeOffset.UtcNow);
        var row = Assert.Single(new HistoryStore(directory).Snapshot());
        Assert.NotNull(row.Evidence);
        Assert.Null(row.Sold);
        Assert.DoesNotContain("SECRET", File.ReadAllText(Path.Combine(directory, "history.json")));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
