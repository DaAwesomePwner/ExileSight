using ExileSight.Core.Items;
using ExileSight.Core.Features;
using ExileSight.Platform.Abstractions.Shell;

namespace ExileSight.Features.Omni;

public sealed record OmniMenuOrigin(long PressId, int X, int Y);
public sealed record OmniMenuPresentation(OmniMenuOrigin Origin, IReadOnlyList<OmniLink> Links, IReadOnlyList<ItemMenuEntry> Entries);

public sealed class OmniMenu
{
    // Composition supplies the shared Prices setting when available. Standard is the fallback.
    public Func<string?> League { get; set; } = () => null;
    public event Action<IReadOnlyList<OmniLink>>? Opened;
    public Func<IReadOnlyList<IItemMenuProvider>> Providers { get; set; } = () => [];
    public event Action<IReadOnlyList<OmniLink>, IReadOnlyList<ItemMenuEntry>>? EntriesOpened;
    private long currentPressId = -1;
    public Func<(int X, int Y)> CursorPosition { get; set; } = () => (0, 0);
    public event Action<OmniMenuPresentation>? Presented;
    public void BeginPress(long id) => Interlocked.Exchange(ref currentPressId, id);
    public bool IsCurrent(OmniMenuOrigin origin) => Interlocked.Read(ref currentPressId) == origin.PressId;
    public OmniMenuOrigin Capture(long id)
    {
        var cursor = CursorPosition();
        return new(id, cursor.X, cursor.Y);
    }
    public Action<string>? ReportFailure { get; set; }
    public void Show(ParsedItem item) => Show(item, Capture(Interlocked.Read(ref currentPressId)));
    public void Show(ParsedItem item, OmniMenuOrigin origin)
    {
        if (!IsCurrent(origin)) return;
        var links = OmniLinks.Build(item, League());
        var entries = ItemMenuEntries.Build(item, Providers(), error => ReportFailure?.Invoke(error.GetType().Name));
        if (!IsCurrent(origin)) return;
        Presented?.Invoke(new(origin, links, entries));
        EntriesOpened?.Invoke(links, entries);
        Opened?.Invoke(links);
    }
    public static void Open(IBrowser browser, OmniLink link) => browser.Open(link.Url, OmniLinks.AllowedHosts);
}
