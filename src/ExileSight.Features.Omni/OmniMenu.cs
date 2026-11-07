using ExileSight.Core.Items;
using ExileSight.Core.Features;
using ExileSight.Platform.Abstractions.Shell;

namespace ExileSight.Features.Omni;

public sealed class OmniMenu
{
    // Composition supplies the shared Prices setting when available. Standard is the fallback.
    public Func<string?> League { get; set; } = () => null;
    public event Action<IReadOnlyList<OmniLink>>? Opened;
    public Func<IReadOnlyList<IItemMenuProvider>> Providers { get; set; } = () => [];
    public event Action<IReadOnlyList<OmniLink>, IReadOnlyList<ItemMenuEntry>>? EntriesOpened;
    public Action<string>? ReportFailure { get; set; }
    public void Show(ParsedItem item)
    {
        var links = OmniLinks.Build(item, League());
        EntriesOpened?.Invoke(links, ItemMenuEntries.Build(item, Providers(), error => ReportFailure?.Invoke(error.GetType().Name)));
        Opened?.Invoke(links);
    }
    public static void Open(IBrowser browser, OmniLink link) => browser.Open(link.Url, OmniLinks.AllowedHosts);
}
