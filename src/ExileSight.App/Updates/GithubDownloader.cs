using System.Net;
using System.Net.Http;
using Velopack.Sources;
using ExileSight.Core.Updates;

namespace ExileSight.App.Updates;

/// <summary>Validate every redirect before sending; release metadata cannot authorize another host.</summary>
internal sealed class GithubDownloader(CancellationToken operationToken) : HttpClientFileDownloader
{
    // Velopack's check API has no token parameter. These overrides carry the check's token
    // through headers AND response-body reads, so cancellation aborts actual network work.
    public override async Task<string> DownloadString(string url, IDictionary<string, string>? headers, double timeout)
    {
        using var client = CreateHttpClient(headers, timeout);
        return await client.GetStringAsync(url, operationToken);
    }

    public override async Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers, double timeout)
    {
        using var client = CreateHttpClient(headers, timeout);
        return await client.GetByteArrayAsync(url, operationToken);
    }

    public override async Task DownloadFile(string url, string targetFile, Action<int> progress,
        IDictionary<string, string>? headers, double timeout, CancellationToken cancelToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(operationToken, cancelToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(timeout));
        await base.DownloadFile(url, targetFile, progress, headers, timeout, deadline.Token);
    }

    protected override Task<T> TryDownloadThenLowercase<T>(Func<string, Task<T>> downloadFunc, string url) =>
        downloadFunc(url); // Never retry cancellation, or corrupt a signed GitHub asset URL.

    protected override HttpClient CreateHttpClient(IDictionary<string, string>? headers, double timeout)
    {
        var client = new HttpClient(new GithubHandler())
        {
            Timeout = TimeSpan.FromMinutes(timeout)
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
                if (!UpdateNetworkPolicy.IsAllowed(uri))
                    throw new HttpRequestException("Update URL is not an allowed GitHub host.");
                using var next = new HttpRequestMessage(HttpMethod.Get, uri);
                foreach (var header in request.Headers)
                    next.Headers.TryAddWithoutValidation(header.Key, header.Value);
                var response = await base.SendAsync(next, cancellationToken);
                if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308)) return response;
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null) throw new HttpRequestException("Update redirect has no location.");
                uri = UpdateNetworkPolicy.Redirect(uri, location, redirects + 1);
            }
            throw new HttpRequestException("Too many update redirects.");
        }
    }
}
