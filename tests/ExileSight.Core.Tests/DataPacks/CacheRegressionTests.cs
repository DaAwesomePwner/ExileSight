using System.Net;
using System.Text;
using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.DataPacks.Loading;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class CacheRegressionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly Clock clock = new();
    private readonly PackContent content = new("poe1/mods", Game.PoE1, PackCodec.SchemaVersion, "synthetic", [], []);
    private string PathName => Path.Combine(directory, "poe1-mods.pack.json");
    private PackManifest Manifest => new(content.PackId, content.Game, content.SchemaVersion, content.PackVersion,
        "unknown", DateTimeOffset.UnixEpoch, new("synthetic", new string('a', 40), []), "synthetic", PackCodec.Hash(PackCodec.ContentBytes(content)));
    private HttpResponseMessage Serve(HttpRequestMessage request) => new(HttpStatusCode.OK)
    {
        Headers = { ETag = new("\"v1\"") },
        Content = new ByteArrayContent(request.RequestUri!.AbsolutePath.EndsWith(".manifest.json", StringComparison.Ordinal)
            ? JsonSerializer.SerializeToUtf8Bytes(Manifest, PackJson.Default.PackManifest) : PackCodec.ContentBytes(content))
    };

    [Fact]
    public async Task ExactLfBytesSurviveCacheAndCrLfMutationIsRejected()
    {
        var bytes = PackCodec.ContentBytes(content);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.NotNull(PackCodec.Validate(Manifest, bytes, content.PackId));
        var changed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\n", "\r\n", StringComparison.Ordinal));
        Assert.Null(PackCodec.Validate(Manifest, changed, content.PackId));
        Assert.NotNull(PackCodec.Validate(Manifest with { ContentSha256 = PackCodec.Hash(changed) }, changed, content.PackId));
        Directory.CreateDirectory(directory);
        using var transport = new GitHubTransport("1.0", () => new Handler(Serve));
        var loader = new PackLoader(directory, transport);
        foreach (var (raw, valid) in new[] { (bytes, true), (changed, false) })
        {
            await File.WriteAllBytesAsync(PathName, JsonSerializer.SerializeToUtf8Bytes(new CachedPack(Manifest, raw), PackJson.Default.CachedPack));
            Assert.Equal(valid, (await loader.ReadLocalAsync(content.PackId)).Pack is not null);
        }
    }

    [Fact]
    public async Task DifferentTagBypassesFreshnessAndDoesNotSendOldEtag()
    {
        var handler = new Handler(request => { Assert.Empty(request.Headers.IfNoneMatch); return Serve(request); });
        using var transport = new GitHubTransport("1.0", () => handler);
        var loader = new PackLoader(directory, transport, timeProvider: clock);
        Assert.NotNull((await loader.RefreshAsync(content.PackId, "v1")).Pack);
        Assert.NotNull((await loader.RefreshAsync(content.PackId, "v2")).Pack);
        Assert.NotNull((await loader.RefreshAsync(content.PackId, "v1")).Pack);
        Assert.Equal(6, handler.Calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(86400)]
    public async Task FuturePersistedCheckAllowsRefresh(int secondsAhead)
    {
        Directory.CreateDirectory(directory);
        var cached = new CachedPack(Manifest, PackCodec.ContentBytes(content), "v1", clock.Now.AddSeconds(secondsAhead));
        await File.WriteAllBytesAsync(PathName, JsonSerializer.SerializeToUtf8Bytes(cached, PackJson.Default.CachedPack));
        var handler = new Handler(Serve);
        using var transport = new GitHubTransport("1.0", () => handler);
        var loader = new PackLoader(directory, transport, timeProvider: clock);
        Assert.NotNull((await loader.RefreshAsync(content.PackId, "v1")).Pack);
        Assert.Equal(2, handler.Calls);
        var refreshed = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(PathName), PackJson.Default.CachedPack)!;
        Assert.Equal(clock.Now, refreshed.CheckedAt);
        await loader.RefreshAsync(content.PackId, "v1");
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrCorruptCacheDoesNotSendEtagAndRetriesUnexpected304(bool corrupt)
    {
        var handler = new Handler(Serve);
        using var transport = new GitHubTransport("1.0", () => handler);
        var loader = new PackLoader(directory, transport, timeProvider: clock);
        await loader.RefreshAsync(content.PackId, "v1");
        if (corrupt) await File.WriteAllTextAsync(PathName, "{}"); else File.Delete(PathName);
        var calls = 0; // A missing or corrupt cache bypasses even a recent successful check.
        handler.Respond = request =>
        {
            Assert.Empty(request.Headers.IfNoneMatch);
            return ++calls == 1 ? new(HttpStatusCode.NotModified) : Serve(request);
        };
        Assert.NotNull((await loader.RefreshAsync(content.PackId, "v1")).Pack);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task CacheDeletedDuringConditionalRequestTriggersUnconditionalRetry()
    {
        var handler = new Handler(Serve);
        using var transport = new GitHubTransport("1.0", () => handler);
        var loader = new PackLoader(directory, transport, timeProvider: clock);
        await loader.RefreshAsync(content.PackId, "v1");
        clock.Now += TimeSpan.FromHours(2);
        handler.Respond = request =>
        {
            if (request.Headers.IfNoneMatch.Any()) { File.Delete(PathName); return new(HttpStatusCode.NotModified); }
            return Serve(request);
        };
        Assert.NotNull((await loader.RefreshAsync(content.PackId, "v1")).Pack);
        Assert.Equal(5, handler.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]
    [InlineData(".......")]
    [InlineData("v..1")]
    [InlineData("v/1")]
    [InlineData("v\\1")]
    [InlineData("v%2F1")]
    public async Task RejectsUnsafeSegmentsBeforeNetwork(string segment)
    {
        var handler = new Handler(Serve);
        using var transport = new GitHubTransport("1.0", () => handler);
        var loader = new PackLoader(directory, transport);
        Assert.Throws<ArgumentException>(() => PackLoader.ValidateSegment(segment));
        await Assert.ThrowsAsync<ArgumentException>(() => loader.RefreshAsync(content.PackId, segment));
        Assert.Equal(0, handler.Calls);
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(Respond(request)); }
    }
}
