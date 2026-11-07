using System.Net;

namespace ExileSight.Core.Pricing;

public sealed record PriceResponse(string? Json, string? ETag, DateTimeOffset? LastModified);

/// <summary>Owns the handler so production cannot silently follow redirects or send cookies.</summary>
public sealed class PriceTransport : IDisposable
{
    private readonly HttpClient client;
    private readonly int maximumBytes;
    public PriceTransport(string version, int maximumBytes = 8 * 1024 * 1024)
        : this(version, null, maximumBytes) { }

    internal PriceTransport(string version, Func<HttpMessageHandler>? handler, int maximumBytes = 8 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        this.maximumBytes = maximumBytes;
        client = new(handler?.Invoke() ?? new SocketsHttpHandler
        { AllowAutoRedirect = false, UseCookies = false, Credentials = null });
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"ExileSight/{version} (+https://github.com/DaAwesomePwner/ExileSight)");
    }

    public static bool IsAllowed(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" &&
        uri.Host == "poe.ninja" && uri.IsDefaultPort && uri.UserInfo.Length == 0;

    public async Task<PriceResponse> GetAsync(Uri uri, string? etag, DateTimeOffset? modified, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        for (var hop = 0; hop <= 5; hop++)
        {
            if (!IsAllowed(uri)) throw new HttpRequestException("Price host is not allowed.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (etag is not null) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            request.Headers.IfModifiedSince = modified;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (response.RequestMessage?.RequestUri is { } final && final != uri)
                throw new HttpRequestException("Handler followed a redirect unexpectedly.");
            if (response.StatusCode == HttpStatusCode.NotModified)
                return new(null, response.Headers.ETag?.ToString() ?? etag, response.Content.Headers.LastModified ?? modified);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                uri = new Uri(uri, response.Headers.Location ?? throw new HttpRequestException("Missing redirect location."));
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > maximumBytes) throw new HttpRequestException("Price response too large.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var limited = new LimitedReadStream(stream, maximumBytes);
            using var document = await System.Text.Json.JsonDocument.ParseAsync(limited, cancellationToken: ct).ConfigureAwait(false);
            return new(document.RootElement.GetRawText(), response.Headers.ETag?.ToString(), response.Content.Headers.LastModified);
        }
        throw new HttpRequestException("Too many redirects.");
    }
    public void Dispose() => client.Dispose();
}
