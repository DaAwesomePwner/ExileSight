using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Shell;
using ExileSight.App.Shell.Pages;
using ExileSight.App.Ui;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Core.Settings;
using ExileSight.Features.ItemInfo;
using System.Text.Json;
#endif
namespace ExileSight.App.Snapshots.Tests;
public sealed class ItemRulesPageSnapshotTests
{
    [WindowsOnlyFact]
    public void Default() => Render("default", false, 1280);
    [WindowsOnlyFact]
    public void WithPreview() => Render("preview", true, 1280);
    [WindowsOnlyFact]
    public void Narrow() => Render("narrow", true, 560);
    private static void Render(string name, bool preview, int width)
    {
#if SNAPSHOT_WINDOWS
        var settings = preview ? new ItemInfoSettings([new("Life", RuleText.Create("+# to maximum Life",
            Highlight.Desired, ["Rings", "Amulets"]))]) : ItemInfoSettings.Default;
        var card = preview ? ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(Item(), settings)) : null;
        Assert.True(File.Exists(SnapshotRenderer.Render(() =>
            ShellBuilder.Build(new("Item rules", "Standard", [], [], null), CardTheme.Default, width < 760,
                settingsPage: ItemRulesPageBuilder.Build(new(settings, new(), card), CardTheme.Default)),
            "item-rules-" + name, new Size(width, 1200))));
#endif
    }
    [WindowsOnlyFact]
    public void SavesClassesTogglesAndProfilesAcrossReopen()
    {
#if SNAPSHOT_WINDOWS
        var directory = Path.Combine(Path.GetTempPath(), "item-rules-" + Guid.NewGuid());
        try
        {
            using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(directory), Path.Combine(directory, "settings.json"),
                SettingsJsonContext.Default.SettingsDocument, () => new());
            SnapshotRenderer.Render(() =>
            {
                var source = new ItemInfoViewModel();
                source.Show(new ItemInfoCalculator().Calculate(Item()));
                source.Close();
                Assert.NotNull(source.LastCopied);
                var host = SettingsPageTestTheme.Apply(new ItemRulesPageHost(store, source, ["+# to maximum Life"]));
                T Find<T>(string name) where T : FrameworkElement => Descendants(host.Content).OfType<T>().Single(e => e.Name == name);
                void Click(string tag)
                {
                    var b = Descendants(host.Content).OfType<Button>().Single(b => Equals(b.Tag, tag));
                    b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
                }
                Find<TextBox>("RuleSearch").Text = "Life";
                Find<ListBox>("RuleResults").SelectedIndex = 0;
                Click("AddRule");
                Find<TextBox>("Classes").Text = "Rings, Amulets";
                Find<CheckBox>("Advanced").IsChecked = false;
                Find<CheckBox>("Level").IsChecked = false;
                Click("SaveRules");
                Assert.False(host.Dirty);
                var saved = store.Snapshot.Features.Sections["iteminfo"]["profiles"].Deserialize<ItemInfoSettings>()!;
                Assert.Equal(2, saved.Profiles[0].Rules.Length);
                Assert.False(saved.ShowItemLevelColumn);
                Assert.False(store.Snapshot.Features.Sections["omni"]["settings"].GetProperty("UseAdvancedCopy").GetBoolean());
                Click("Profile4"); Click("SaveRules");
                Assert.Equal(4, store.Snapshot.Features.Sections["iteminfo"]["profiles"].GetProperty("ActiveProfile").GetInt32());
                store.Flush();
                using var disk = new JsonSettingsStore<SettingsDocument>(new AppPaths(directory), Path.Combine(directory, "settings.json"),
                    SettingsJsonContext.Default.SettingsDocument, () => new());
                Assert.Equal(4, disk.Snapshot.Features.Sections["iteminfo"]["profiles"].GetProperty("ActiveProfile").GetInt32());
                var reopened = SettingsPageTestTheme.Apply(new ItemRulesPageHost(store, source, []));
                Assert.False(Descendants(reopened.Content).OfType<CheckBox>().Single(c => c.Name == "Advanced").IsChecked);
                return reopened;
            }, "item-rules-save", new Size(1280, 1200));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
#endif
    }
#if SNAPSHOT_WINDOWS
    private static ParsedItem Item() => ItemTextParser.Parse(
        "Item Class: Rings\nRarity: Rare\nSynthetic Loop\nSynthetic Ring\n--------\nItem Level: 80\n--------\n+40 to maximum Life",
        Game.PoE1).Item!;
    private static IEnumerable<object> Descendants(object root)
    {
        yield return root;
        if (root is DependencyObject d)
            foreach (var child in LogicalTreeHelper.GetChildren(d))
                foreach (var nested in Descendants(child)) yield return nested;
    }
#endif
}
