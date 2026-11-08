using ExileSight.Core.DataPacks;
using ExileSight.Features.ItemInfo;
namespace ExileSight.App.Shell.Pages;

/// <summary>One background decode per shell, shared by every page instance.</summary>
public sealed class ItemRulesTranslations(Func<StatTranslation[]> read)
{
    private readonly Lazy<Task<string[]>> cached = new(() => Task.Run(() => read()
        .Where(t => !t.Hidden).SelectMany(t => t.English).Select(v => v.Text).Distinct().Order().ToArray()));
    public Task<string[]> LoadAsync() => cached.Value;
}
public sealed record ItemRulesSearchResult(string Template)
{
    public override string ToString() => RuleText.Display(Template);
}
