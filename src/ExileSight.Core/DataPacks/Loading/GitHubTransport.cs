using System.Net;

namespace ExileSight.Core.DataPacks.Loading;

/// <summary>Owns a non-redirecting client. The internal handler factory is solely a test seam.</summary>
public sealed class GitHubTransport : IDisposable
{
    private readonly HttpClient client;
    private readonly string version;
    private readonly int maximumBytes;
    public GitHubTransport(string version, int maximumBytes = 32 * 1024 * 1024)
        : this(version, null, maximumBytes) { }

    internal GitHubTransport(string version, Func<HttpMessageHandler>? testHandlerFactory, int maximumBytes = 32 * 1024 * 1024)
    {
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        this.version = version;
        this.maximumBytes = maximumBytes;
        client = new(testHandlerFactory?.Invoke() ?? new SocketsHttpHandler { AllowAutoRedirect = false });
    }
    public static bool IsAllowed(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" &&
        uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
        uri.Host is "github.com" or "objects.githubusercontent.com" or "release-assets.githubusercontent.com";

    public async Task<HttpResponseMessage> GetAsync(Uri uri, string? etag, CancellationToken cancellationToken)
    {
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            if (!IsAllowed(uri)) throw new HttpRequestException("Pack host is not allowed.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd($"ExileSight/{version} (+https://github.com/DaAwesomePwner/ExileSight)");
            if (etag is not null) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.RequestMessage?.RequestUri is { } final && final != uri)
            {
                response.Dispose();
                throw new HttpRequestException("Handler followed a redirect unexpectedly.");
            }
            if ((int)response.StatusCode is >= 300 and < 400 && response.StatusCode != HttpStatusCode.NotModified)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null) throw new HttpRequestException("Missing redirect location.");
                uri = new Uri(uri, location);
                continue;
            }
            return response;
        }
        throw new HttpRequestException("Too many redirects.");
    }

    public async Task<byte[]> ReadBytesAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > maximumBytes) throw new HttpRequestException("Pack asset exceeds size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, maximumBytes - output.Length + 1)), token);
            if (read == 0) return output.ToArray();
            if (output.Length + read > maximumBytes) throw new HttpRequestException("Pack asset exceeds size limit.");
            output.Write(buffer, 0, read);
        }
    }
    public void Dispose() => client.Dispose();
}
