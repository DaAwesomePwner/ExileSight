using ExileSight.Core.DataPacks.Loading;

namespace ExileSight.Core.Updates;

public static class UpdateNetworkPolicy
{
    // Reuse the pack transport's scheme, credentials, port and release-host restrictions.
    // Its host set deliberately excludes the API host; only updater checks need that host.
    public static bool IsAllowed(Uri uri) => GitHubTransport.IsAllowed(uri) ||
        uri.IsAbsoluteUri && uri.Host == "api.github.com" &&
        GitHubTransport.IsAllowed(new UriBuilder(uri) { Host = "github.com" }.Uri);

    public static Uri Redirect(Uri source, Uri location, int hop)
    {
        if (!IsAllowed(source) || hop is < 1 or > 10)
            throw new ArgumentException("Invalid update redirect source or hop count.");
        var target = new Uri(source, location);
        if (!IsAllowed(target)) throw new ArgumentException("Update redirect is not an allowed GitHub URL.");
        return target;
    }
}
