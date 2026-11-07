using ExileSight.Core.Items;
using ExileSight.Platform.Abstractions.Shell;

namespace ExileSight.Features.Omni;

public sealed class OmniMenu
{
    // Composition supplies the shared Prices setting when available. Standard is the fallback.
    public Func<string?> League { get; set; } = () => null;
    public event Action<IReadOnlyList<OmniLink>>? Opened;
    public void Show(ParsedItem item) => Opened?.Invoke(OmniLinks.Build(item, League()));
    public static void Open(IBrowser browser, OmniLink link) => browser.Open(link.Url, OmniLinks.AllowedHosts);
}
