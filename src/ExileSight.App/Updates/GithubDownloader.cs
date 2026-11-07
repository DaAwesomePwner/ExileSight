using System.Net;
using System.Net.Http;
using Velopack.Sources;

namespace ExileSight.App.Updates;

/// <summary>Validate every redirect before sending; release metadata cannot authorize another host.</summary>
internal sealed class GithubDownloader : HttpClientFileDownloader
{
    protected override HttpClient CreateHttpClient(IDictionary<string, string>? headers, double timeout)
    {
        var client = new HttpClient(new GithubHandler())
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ExileSight/1.0");
        foreach (var header in headers ?? new Dictionary<string, string>())
            client.DefaultRequestHeaders.Add(header.Key, header.Value);
        return client;
    }

    private sealed class GithubHandler : DelegatingHandler
    {
        public GithubHandler() : base(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        }) { }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            for (var redirects = 0; redirects <= 10; redirects++)
            {
                if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                    uri.Host is not ("github.com" or "api.github.com" or "objects.githubusercontent.com" or "release-assets.githubusercontent.com"))
                    throw new HttpRequestException("Update URL is not an allowed GitHub host.");
                using var next = new HttpRequestMessage(HttpMethod.Get, uri);
                foreach (var header in request.Headers)
                    next.Headers.TryAddWithoutValidation(header.Key, header.Value);
                var response = await base.SendAsync(next, cancellationToken);
                if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308)) return response;
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null) throw new HttpRequestException("Update redirect has no location.");
                uri = new Uri(uri, location);
            }
            throw new HttpRequestException("Too many update redirects.");
        }
    }
}
