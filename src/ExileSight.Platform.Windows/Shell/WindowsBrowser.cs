using System.Diagnostics;
using ExileSight.Platform.Abstractions.Shell;

namespace ExileSight.Platform.Windows.Shell;

public sealed class WindowsBrowser : IBrowser
{
    public void Open(string url, IReadOnlySet<string> allowedHosts)
    {
        var validated = BrowserUrl.Validate(url, allowedHosts);
        using var process = Process.Start(new ProcessStartInfo(validated.AbsoluteUri) { UseShellExecute = true });
    }
}
