namespace ExileSight.Platform.Abstractions.Shell;

public interface IBrowser
{
    void Open(string url, IReadOnlySet<string> allowedHosts);
}
public static class BrowserUrl
{
    public static Uri Validate(string url, IEnumerable<string> allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Any(char.IsControl) || url.Contains('\\') ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.HostNameType != UriHostNameType.Dns ||
            !allowedHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Browser URL must be HTTPS on an explicitly allowed host", nameof(url));
        return uri;
    }
}
