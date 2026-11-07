using System.Net;
using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.DataPacks.Loading;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class PackLoaderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ExileSight-packs-" + Guid.NewGuid());
    private readonly FakeHandler handler = new();
    private readonly GitHubTransport transport;
    public PackLoaderTests() { Directory.CreateDirectory(directory); transport = new("1.0", () => handler); }
    private PackLoader Loader(string? fallback = null) => new(directory, transport, fallback);
    private string CachePath => Path.Combine(directory, "poe1-bases.pack.json");
    private static DataPack Pack()
    {
        var content = new PackContent("poe1/bases", Game.PoE1, PackCodec.SchemaVersion, "test", [], []);
        return new(new("poe1/bases", Game.PoE1, PackCodec.SchemaVersion, "test", "unknown", DateTimeOffset.UnixEpoch,
            new("synthetic", new string('a', 40), []), "synthetic", PackCodec.Hash(PackCodec.ContentBytes(content))), content);
    }
    private async Task Cache(DataPack pack) => await File.WriteAllBytesAsync(CachePath, JsonSerializer.SerializeToUtf8Bytes(new CachedPack(pack.Manifest, PackCodec.ContentBytes(pack.Content), "data-v1", DateTimeOffset.UtcNow), PackJson.Default.CachedPack));
    private async Task Stale()
    {
        var cached = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(CachePath), PackJson.Default.CachedPack)!;
        await File.WriteAllBytesAsync(CachePath, JsonSerializer.SerializeToUtf8Bytes(cached with { CheckedAt = DateTimeOffset.UtcNow.AddDays(-2) }, PackJson.Default.CachedPack));
    }
    private void Serve(DataPack pack, bool corrupt = false)
    {
        handler.Respond = request => new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(request.RequestUri!.AbsolutePath.EndsWith(".manifest.json", StringComparison.Ordinal)
                ? JsonSerializer.SerializeToUtf8Bytes(pack.Manifest, PackJson.Default.PackManifest)
                : corrupt ? [1, 2, 3] : PackCodec.ContentBytes(pack.Content))
        };
    }

    [Fact]
    public async Task StartupMissDoesNotUseNetwork()
    {
        Assert.Equal(PackAvailability.Unavailable, (await Loader().ReadLocalAsync("poe1/bases")).Availability);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task FreshCacheDoesNotUseNetworkAndUnknownTierSurvives()
    {
        var pack = Pack();
        var content = pack.Content with { Bases = [new("invented", "Paper", "Paper", [], 1, [], [], [])] };
        pack = new(pack.Manifest with { ContentSha256 = PackCodec.Hash(PackCodec.ContentBytes(content)) }, content);
        await Cache(pack);
        var result = await Loader().RefreshAsync("poe1/bases", "data-v1");
        Assert.Null(Assert.Single(result.Pack!.Content.Bases).UniqueDropTier);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task OfflineStaleCacheRemainsAvailableAndChecksAreThrottled()
    {
        await Cache(Pack());
        await Stale();
        handler.Respond = _ => throw new HttpRequestException("offline");
        var loader = Loader();
        Assert.NotNull((await loader.RefreshAsync("poe1/bases", "data-v1")).Pack);
        Assert.NotNull((await loader.RefreshAsync("poe1/bases", "data-v1")).Pack);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task DownloadsAndAtomicallyPublishesValidatedEnvelope()
    {
        Serve(Pack());
        Assert.NotNull((await Loader().RefreshAsync("poe1/bases", "data-v1")).Pack);
        Assert.NotNull((await Loader().ReadLocalAsync("poe1/bases")).Pack);
        Assert.Single(Directory.GetFiles(directory));
        Assert.Equal(2, handler.Calls);
        Assert.Contains("ExileSight/1.0", handler.UserAgent);
    }

    [Fact]
    public async Task ChecksumMismatchDoesNotReplaceOldCache()
    {
        await Cache(Pack());
        await Stale();
        var original = await File.ReadAllBytesAsync(CachePath);
        Serve(Pack(), corrupt: true);
        Assert.NotNull((await Loader().RefreshAsync("poe1/bases", "data-v1")).Pack);
        Assert.Equal(original, await File.ReadAllBytesAsync(CachePath));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("version")]
    [InlineData("game")]
    [InlineData("hash")]
    [InlineData("id")]
    public async Task RejectsInvalidCachedManifest(string mismatch)
    {
        var pack = Pack();
        var m = pack.Manifest;
        m = mismatch switch
        {
            "schema" => m with { SchemaVersion = 999 }, "version" => m with { PackVersion = "other" },
            "game" => m with { Game = Game.PoE2 }, "hash" => m with { ContentSha256 = "bad" },
            _ => m with { PackId = "poe1/mods" }
        };
        await Cache(pack with { Manifest = m });
        Assert.Null((await Loader().ReadLocalAsync("poe1/bases")).Pack);
    }

    [Fact]
    public async Task BundledFallbackWorksAndCorruptCacheIsUnavailable()
    {
        await File.WriteAllTextAsync(CachePath, "invalid json");
        Assert.Null((await Loader().ReadLocalAsync("poe1/bases")).Pack);
        var fallback = Path.Combine(directory, "fallback");
        Directory.CreateDirectory(fallback);
        await File.WriteAllBytesAsync(Path.Combine(fallback, "poe1-bases.pack.json"), JsonSerializer.SerializeToUtf8Bytes(new CachedPack(Pack().Manifest, PackCodec.ContentBytes(Pack().Content)), PackJson.Default.CachedPack));
        Assert.NotNull((await Loader(fallback).ReadLocalAsync("poe1/bases")).Pack);
    }

    [Theory]
    [InlineData("https://pathofexile.com/no")]
    [InlineData("http://github.com/no")]
    [InlineData("https://github.com.evil.invalid/no")]
    [InlineData("https://user@github.com/no")]
    [InlineData("https://github.com:444/no")]
    public async Task RejectsHostsBeforeSending(string uri)
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => transport.GetAsync(new(uri), null, default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task RedirectToUnapprovedHostIsNeverRequested()
    {
        handler.Respond = _ => new(HttpStatusCode.Redirect) { Headers = { Location = new("https://example.invalid/data") } };
        Assert.Null((await Loader().RefreshAsync("poe1/bases", "data-v1")).Pack);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task TransportSendsConditionalHeaderAndAcceptsNotModified()
    {
        handler.Respond = request =>
        {
            Assert.Equal("\"synthetic\"", request.Headers.IfNoneMatch.Single().ToString());
            return new(HttpStatusCode.NotModified);
        };
        using var response = await transport.GetAsync(new("https://github.com/test"), "\"synthetic\"", default);
        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    public void Dispose() { transport.Dispose(); Directory.Delete(directory, true); }
    private sealed class FakeHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string UserAgent { get; private set; } = "";
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => throw new HttpRequestException("offline");
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(Respond(request));
        }
    }
}
