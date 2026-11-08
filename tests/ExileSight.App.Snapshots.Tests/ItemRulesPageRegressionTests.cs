using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Shell.Pages;
using ExileSight.Core.Settings;
using ExileSight.Core.DataPacks;
using ExileSight.Features.ItemInfo;
using ExileSight.Platform.Abstractions.Storage;
#endif
namespace ExileSight.App.Snapshots.Tests;
public sealed class ItemRulesPageRegressionTests
{
    [WindowsOnlyFact]
    public void StableIdentityAndDuplicateAddPreserveScopes()
    {
#if SNAPSHOT_WINDOWS
        var editor = new ItemRulesEditor(new([new("Test", [
            new("+0 to Armour", Highlight.Desired, "Body Armours"),
            new("+0 to Armour", Highlight.Undesired, "Shields"),
            new("0% increased Armour", Highlight.Desired)])]));
        var rows = editor.Rows;
        editor.Capture(rows.Select(r => r with { Match = Highlight.Desired }));
        editor.Remove(rows[1].Id);
        Assert.Equal(new[] { rows[0].Id, rows[2].Id }, editor.Rows.Select(r => r.Id));
        editor.Add("+{0} to Armour");
        var saved = editor.Snapshot().Profiles[0].Rules;
        Assert.Equal(2, saved.Length);
        Assert.Equal("Body Armours", saved[0].ItemClass);
        Assert.DoesNotContain(saved, r => r.Text == "+0 to Armour" && r.ItemClass is null);
#endif
    }
    [WindowsOnlyFact]
    public void RegroupRoundTripAndEditingDoNotChangeActiveProfile()
    {
#if SNAPSHOT_WINDOWS
        var rules = RuleText.Create("+# to Armour", Highlight.Desired, ["Body Armours", "Shields"]);
        var editor = new ItemRulesEditor(new([new("First", rules), new("Second", [])]));
        Assert.Single(editor.Rows);
        Assert.Equal(rules, editor.Snapshot().Profiles[0].Rules);
        editor.Editing = 1;
        Assert.Equal(0, editor.Snapshot().ActiveProfile);
        editor.Activate();
        Assert.Equal(1, editor.Snapshot().ActiveProfile);
        var mixed = ItemRulesRow.From(new("Mixed", [.. rules, new("+0 to Armour", Highlight.Desired)]));
        Assert.Equal(2, mixed.Length); // Global and restricted scopes never collapse.
#endif
    }
    [WindowsOnlyFact]
    public async Task TranslationsLoadOnceOffCallerAndKeepTemplateMatching()
    {
#if SNAPSHOT_WINDOWS
        var calls = 0;
        var cache = new ItemRulesTranslations(() =>
        {
            Assert.True(Thread.CurrentThread.IsThreadPoolThread);
            Interlocked.Increment(ref calls);
            return [new("synthetic", ["armour"], [new("+{0} to Armour", [], [], [])])];
        });
        var first = cache.LoadAsync();
        Assert.Same(first, cache.LoadAsync());
        var templates = await first;
        Assert.Equal(1, calls);
        Assert.Equal("+# to Armour", new ItemRulesSearchResult(templates.Single()).ToString());
        Assert.Equal(ModRuleKey.Normalize("+25 to Armour"),
            ModRuleKey.Normalize(RuleText.Create(templates.Single(), Highlight.Desired).Single().Text));
#else
        await Task.CompletedTask;
#endif
    }
    [WindowsOnlyFact]
    public void InvalidScopeWritesNothingAndDiscardRestoresSavedDraft()
    {
#if SNAPSHOT_WINDOWS
        var directory = Path.Combine(Path.GetTempPath(), "item-rules-regression-" + Guid.NewGuid());
        try
        {
            using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(directory), Path.Combine(directory, "settings.json"),
                SettingsJsonContext.Default.SettingsDocument, () => new());
            SnapshotRenderer.Render(() =>
            {
                var host = SettingsPageTestTheme.Apply(new ItemRulesPageHost(store, null, ["+# to Armour"]));
                T Find<T>(string name) where T : FrameworkElement => Descendants(host.Content).OfType<T>().Single(e => e.Name == name);
                void Click(string tag)
                {
                    var b = Descendants(host.Content).OfType<Button>().Single(b => Equals(b.Tag, tag));
                    b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
                }
                Find<TextBox>("RuleSearch").Text = "Armour";
                Find<ListBox>("RuleResults").SelectedIndex = 0; Click("AddRule");
                Find<TextBox>("Classes").Text = "Body Armour";
                Click("SaveRules");
                Assert.Empty(store.Snapshot.Features.Sections);
                Assert.Contains("Body Armour", Find<TextBlock>("RulesNotice").Text);
                Find<TextBox>("Classes").Text = "body armours, shields";
                Click("SaveRules");
                var saved = store.Snapshot.Features.Sections["iteminfo"]["profiles"].Deserialize<ItemInfoSettings>()!;
                Assert.Equal(new[] { "Body Armours", "Shields" }, saved.Profiles[0].Rules.Select(r => r.ItemClass));
                Find<TextBox>("Classes").Text = "rings";
                Find<CheckBox>("Advanced").IsChecked = false;
                Click("Profile1"); Click("ActivateRules");
                Click("DiscardRules");
                Assert.False(host.Dirty);
                Assert.Equal("Body Armours, Shields", Find<TextBox>("Classes").Text);
                Assert.True(Find<CheckBox>("Advanced").IsChecked);
                Click("SaveRules");
                var after = store.Snapshot.Features.Sections["iteminfo"]["profiles"].Deserialize<ItemInfoSettings>()!;
                Assert.Equal(0, after.ActiveProfile);
                Assert.Equal(saved.Profiles[0].Rules, after.Profiles[0].Rules);
                return host;
            }, "item-rules-validation-discard", new Size(1280, 1200));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
#endif
    }
    [WindowsOnlyFact]
    public void JsonValidationErrorsBecomeNoticesInBothSettingsHosts()
    {
#if SNAPSHOT_WINDOWS
        SnapshotRenderer.Render(() =>
        {
            using var store = new RejectingStore();
            var item = SettingsPageTestTheme.Apply(new ItemRulesPageHost(store, null, []));
            var overlay = SettingsPageTestTheme.Apply(new OverlayPageHost(store, 16d / 9));
            foreach (var (host, tag, notice) in new (ContentControl, string, string)[]
                { (item, "SaveRules", "RulesNotice"), (overlay, "SaveOverlay", "OverlayNotice") })
            {
                var button = Descendants(host.Content).OfType<Button>().Single(b => Equals(b.Tag, tag));
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
                Assert.Contains("Not saved.", Descendants(host.Content).OfType<TextBlock>().Single(t => t.Name == notice).Text);
            }
            return item;
        }, "item-rules-json-rejection", new Size(1280, 1200));
#endif
    }
#if SNAPSHOT_WINDOWS
    private sealed class RejectingStore : ISettingsStore<SettingsDocument>
    {
        public SettingsDocument Snapshot => new();
        public bool IsReadOnly => false;
        public bool IsReadOnlyNewerSchema => false;
        public IReadOnlyList<string> Warnings => [];
        public event EventHandler<SettingsChangedEventArgs>? Changed { add { } remove { } }
        public void Update(string section, Action<SettingsDocument> update) => throw new JsonException("Synthetic validation failure");
        public void Flush() { }
        public void Dispose() { }
    }
#endif
#if SNAPSHOT_WINDOWS
    private static IEnumerable<object> Descendants(object root)
    {
        yield return root;
        if (root is DependencyObject d)
            foreach (var child in LogicalTreeHelper.GetChildren(d))
                foreach (var nested in Descendants(child)) yield return nested;
    }
#endif
}
