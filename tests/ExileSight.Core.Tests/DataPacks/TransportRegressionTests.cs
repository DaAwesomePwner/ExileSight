using System.Net;
using ExileSight.Core.DataPacks.Loading;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class TransportRegressionTests
{
    [Fact]
    public async Task UnexpectedHandlerRedirectIsDetected()
    {
        using var transport = new GitHubTransport("1.0", () => new Handler(_ => new(HttpStatusCode.OK)
        { RequestMessage = new(HttpMethod.Get, "https://outside.invalid/asset") }));
        await Assert.ThrowsAsync<HttpRequestException>(() => transport.GetAsync(new("https://github.com/asset"), null, default));
    }
    [Fact]
    public async Task ReleaseAssetHostIsAllowedAndRedirectLoopIsBounded()
    {
        Assert.True(GitHubTransport.IsAllowed(new("https://release-assets.githubusercontent.com/asset")));
        var handler = new Handler(_ => new(HttpStatusCode.Redirect)
        { Headers = { Location = new("https://release-assets.githubusercontent.com/asset") } });
        using var transport = new GitHubTransport("1.0", () => handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => transport.GetAsync(new("https://github.com/asset"), null, default));
        Assert.Equal(6, handler.Calls);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OversizeAssetsFailWithOrWithoutContentLength(bool knownLength)
    {
        using var stream = new UnseekableStream(new byte[100]);
        var handler = new Handler(_ => new(HttpStatusCode.OK)
        { Content = knownLength ? new ByteArrayContent(new byte[100]) : new StreamContent(stream) });
        using var transport = new GitHubTransport("1.0", () => handler, maximumBytes: 16);
        using var response = await transport.GetAsync(new("https://github.com/asset"), null, default);
        await Assert.ThrowsAsync<HttpRequestException>(() => transport.ReadBytesAsync(response, default));
        Assert.True(stream.BytesRead <= 17);
    }
    [Fact]
    public async Task ExactSizeLimitSucceeds()
    {
        using var transport = new GitHubTransport("1.0", () => new Handler(_ => new(HttpStatusCode.OK)
        { Content = new ByteArrayContent(new byte[16]) }), maximumBytes: 16);
        using var response = await transport.GetAsync(new("https://github.com/asset"), null, default);
        Assert.Equal(16, (await transport.ReadBytesAsync(response, default)).Length);
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(respond(request)); }
    }
    private sealed class UnseekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int BytesRead { get; private set; }
        public override bool CanSeek => false;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += count;
            return count;
        }
    }
}
