using System.Net;
using System.Text;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Pricing;

public sealed class TransportReviewTests
{
    [Fact]
    public async Task UnknownLengthResponseStillEnforcesCap()
    {
        using var transport = new PriceTransport("1.0", () => new CacheReviewTests.Handler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ChunkedContent() })), 32);
        await Assert.ThrowsAsync<HttpRequestException>(() => transport.GetAsync(PriceEndpoints.Leagues, null, null, default));
    }
    [Fact]
    public async Task InvalidRedirectAndDecimalOverflowProduceDiagnostics()
    {
        foreach (var invalidRedirect in new[] { true, false })
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                using var transport = new PriceTransport("1.0", () => new CacheReviewTests.Handler(_ =>
                {
                    if (invalidRedirect) throw new UriFormatException("bad Location");
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                            {"core":{"primary":"divine","rates":{"chaos":200}},
                             "lines":[{"id":"x","primaryValue":79228162514264337593543950335}]}
                            """)
                    });
                }));
                var result = await new PriceService(new AppPaths(root), new PricingTests.Clock(), transport)
                    .GetAsync("Standard", "Currency", TimeSpan.Zero);
                Assert.True(result.IsStale);
                Assert.Contains(invalidRedirect ? "UriFormatException" : "OverflowException", result.Diagnostic);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
    private sealed class ChunkedContent : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            foreach (var chunk in new[] { "{\"data\":\"", new string('a', 100), "\"}" })
                await stream.WriteAsync(Encoding.UTF8.GetBytes(chunk));
        }
    }
}
