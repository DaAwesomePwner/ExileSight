using System.Net;

namespace ExileSight.Core.DataPacks.Loading;

/// <summary>Injected clients MUST disable automatic redirects; redirects are validated before each request.</summary>
public sealed class GitHubTransport(HttpClient client, string version)
{
    public static bool IsAllowed(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" &&
        uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
        uri.Host is "github.com" or "objects.githubusercontent.com";

    public async Task<HttpResponseMessage> GetAsync(Uri uri, string? etag, CancellationToken cancellationToken)
    {
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            if (!IsAllowed(uri)) throw new HttpRequestException("Pack host is not allowed.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd($"ExileSight/{version} (+https://github.com/DaAwesomePwner/ExileSight)");
            if (etag is not null) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.RequestMessage?.RequestUri is { } final && final != uri)
            {
                response.Dispose();
                throw new HttpRequestException("Automatic redirects must be disabled.");
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
}
