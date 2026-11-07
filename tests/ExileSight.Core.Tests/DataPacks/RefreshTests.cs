using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.DataPacks.Loading;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class RefreshTests
{
    [Fact]
    public async Task HourlyRefreshUsesEtagAndNotModifiedKeepsCache()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var content = new PackContent("poe1/mods", Game.PoE1, PackCodec.SchemaVersion, "synthetic", [], []);
        var manifest = new PackManifest(content.PackId, Game.PoE1, PackCodec.SchemaVersion, content.PackVersion, "unknown",
            DateTimeOffset.UnixEpoch, new("synthetic", new string('b', 40), []), "synthetic", PackCodec.Hash(PackCodec.ContentBytes(content)));
        var clock = new Clock();
        using var handler = new Handler(request =>
        {
            if (request.Headers.IfNoneMatch.Any())
            {
                Assert.Equal("\"v1\"", request.Headers.IfNoneMatch.Single().ToString());
                return new(HttpStatusCode.NotModified);
            }
            return new(HttpStatusCode.OK)
            {
                Headers = { ETag = new EntityTagHeaderValue("\"v1\"") },
                Content = new ByteArrayContent(request.RequestUri!.AbsolutePath.EndsWith(".manifest.json", StringComparison.Ordinal)
                    ? JsonSerializer.SerializeToUtf8Bytes(manifest, PackJson.Default.PackManifest) : PackCodec.ContentBytes(content))
            };
        });
        using var transport = new GitHubTransport("1.0", () => handler);
        var loader = new PackLoader(path, transport, timeProvider: clock);
        try
        {
            Assert.NotNull((await loader.RefreshAsync("poe1/mods", "v1")).Pack);
            clock.Now += TimeSpan.FromMinutes(59);
            await loader.RefreshAsync("poe1/mods", "v1");
            Assert.Equal(2, handler.Calls);
            clock.Now += TimeSpan.FromMinutes(2);
            Assert.NotNull((await loader.RefreshAsync("poe1/mods", "v1")).Pack);
            Assert.Equal(3, handler.Calls);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    [Fact]
    public async Task AllowedRedirectIsFollowed()
    {
        using var handler = new Handler(request => request.RequestUri!.Host == "github.com"
            ? new(HttpStatusCode.Redirect) { Headers = { Location = new("https://objects.githubusercontent.com/test") } }
            : new(HttpStatusCode.OK));
        using var transport = new GitHubTransport("1.0", () => handler);
        using var response = await transport.GetAsync(new("https://github.com/test"), null, default);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.Calls);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }
}
