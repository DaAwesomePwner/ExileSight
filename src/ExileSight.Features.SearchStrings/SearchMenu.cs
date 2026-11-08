using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.SearchStrings;

// Behaviour reference: Exile-UI modules/search-strings.ahk. Wheel changes display only.
public sealed class SearchMenu
{
    private readonly TimeProvider clock;
    private long preparedAt;
    public SearchMenu() : this(TimeProvider.System) { }
    public SearchMenu(TimeProvider clock) => this.clock = clock;
    private readonly object sync = new();
    private Dictionary<string, SavedSearch[]> lists = [];
    private string[][] lines = [];
    private int[] indexes = [];
    private string? pending;
    public string? Vendor { get; private set; }
    public string? Notice { get; internal set; }
    public IReadOnlyList<string> Vendors => lists.Keys.ToArray();
    public IReadOnlyList<SavedSearch> Entries => Vendor is null ? [] : lists[Vendor];
    public event Action? Opened;
    internal void Configure(Dictionary<string, SavedSearch[]> value) { lock (sync) lists = value; }
    public void Open()
    {
        lock (sync) { pending = null; Vendor = null; lines = []; indexes = []; }
        Opened?.Invoke();
    }
    public void ChooseVendor(string vendor)
    {
        lock (sync)
        {
            lines = lists[vendor].Select(SearchSettings.Compile).ToArray();
            indexes = new int[lines.Length]; Vendor = vendor; pending = null;
        }
    }
    public string Text(int entry) { lock (sync) return lines[entry][indexes[entry]]; }
    public void Cycle(int entry, int direction)
    {
        lock (sync)
            indexes[entry] = (indexes[entry] + Math.Sign(direction) + lines[entry].Length) % lines[entry].Length;
    }
    // Called immediately before host click redemption; this does not itself grant input authority.
    public void Prepare(int entry)
    {
        lock (sync)
        {
            pending = null;
            var text = Text(entry);
            preparedAt = clock.GetTimestamp();
            pending = text;
        }
    }
    public void Cancel() { lock (sync) pending = null; }
    internal ActionResult Paste(IntentToken? token, Func<IntentToken?, GameAction, ActionResult> execute)
    {
        string? text;
        lock (sync)
        {
            text = clock.GetElapsedTime(preparedAt) < TimeSpan.FromSeconds(1.5) ? pending : null;
            pending = null;
        }
        if (token is null) return new(ActionOutcome.MissingToken);
        return text is null ? new(ActionOutcome.Failed, "Select a search first")
            : execute(token, new GameAction.PasteSearch(text));
    }
}
