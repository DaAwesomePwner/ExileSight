using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Features.ItemInfo;
namespace ExileSight.App.Shell.Pages;

/// <summary>Share successful decodes until the stats pack changes; retry unavailable data on the next request.</summary>
public sealed class ItemRulesTranslations : IDisposable
{
    private readonly Func<StatTranslation[]> read;
    private readonly IPackReader? packs;
    private readonly object sync = new();
    private Task<string[]>? cached;
    public event Action? Changed;
    public ItemRulesTranslations(Func<StatTranslation[]> read, IPackReader? packs = null)
    {
        this.read = read; this.packs = packs;
        if (packs is not null) packs.Changed += PackChanged;
    }
    public Task<string[]> LoadAsync()
    {
        lock (sync)
        {
            if (cached is { IsCompleted: true } &&
                (!cached.IsCompletedSuccessfully || cached.Result.Length == 0)) cached = null;
            return cached ??= Task.Run(() => read().Where(t => !t.Hidden).SelectMany(t => t.English)
                .Select(v => v.Text).Distinct().Order().ToArray());
        }
    }
    private void PackChanged(PackChange change)
    {
        if (change.PackId != "poe1/stats") return;
        lock (sync) cached = null;
        Changed?.Invoke();
    }
    public void Dispose()
    {
        if (packs is not null) packs.Changed -= PackChanged;
    }
}
public sealed record ItemRulesSearchResult(string Template)
{
    public override string ToString() => RuleText.Display(Template);
}
