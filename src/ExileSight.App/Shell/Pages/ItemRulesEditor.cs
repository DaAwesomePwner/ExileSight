using ExileSight.Features.ItemInfo;
namespace ExileSight.App.Shell.Pages;

/// <summary>Draft rows retain identity until reload; only serialization expands class scopes.</summary>
public sealed class ItemRulesEditor
{
    public ItemInfoSettings Settings { get; set; }
    public int Editing { get; set; }
    private readonly List<ItemRulesRow>[] rows;
    public ItemRulesEditor(ItemInfoSettings settings)
    {
        Settings = settings;
        Editing = settings.ActiveProfile;
        rows = Enumerable.Range(0, 5).Select(i => i < settings.Profiles.Length
            ? ItemRulesRow.From(settings.Profiles[i]).ToList() : []).ToArray();
    }
    public ItemRulesRow[] Rows => rows[Editing].ToArray();
    public void Capture(IEnumerable<ItemRulesRow> updated) => rows[Editing] = updated.ToList();
    public void Remove(Guid id) => rows[Editing].RemoveAll(r => r.Id == id);
    public void Add(string template)
    {
        foreach (var line in RuleText.Lines(template))
        {
            // A duplicate search selection must not turn an existing class-specific rule into a global rule.
            if (rows[Editing].Any(r => !r.Implicit && !r.Enchant &&
                ModRuleKey.Normalize(r.Text) == ModRuleKey.Normalize(line))) continue;
            rows[Editing].Add(new(line, "", Highlight.Desired, false, false));
        }
    }
    public void Activate() => Settings = Settings with { ActiveProfile = Editing };
    public ItemInfoSettings Snapshot(bool preview = false)
    {
        var count = Math.Max(Settings.Profiles.Length, Math.Max((preview ? Editing : Settings.ActiveProfile) + 1,
            Array.FindLastIndex(rows, r => r.Count > 0) + 1));
        var profiles = Enumerable.Range(0, count).Select(i =>
        {
            var name = i < Settings.Profiles.Length ? Settings.Profiles[i].Name : $"Profile {i + 1}";
            try
            {
                return new HighlightProfile(name, rows[i].SelectMany(r =>
                    RuleText.Create(r.Text, r.Match, r.Classes.Split(','), r.Implicit, r.Enchant)).Distinct().ToArray());
            }
            catch (ArgumentException error)
            { throw new ArgumentException($"Profile {i + 1} ({name}): {error.Message}", error); }
        }).ToArray();
        return (Settings with { Profiles = profiles, ActiveProfile = preview ? Editing : Settings.ActiveProfile }).Validate();
    }
}
