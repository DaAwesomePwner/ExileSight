using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ExileSight.App.Shell.Pages;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Settings;
using ExileSight.Features.ItemInfo;
#endif
namespace ExileSight.App.Snapshots.Tests;

public sealed class ItemRulesPagePolishTests
{
    [WindowsOnlyFact]
    public async Task EmptyAndFailedLoadsRetryButSuccessfulLoadsAreShared()
    {
#if SNAPSHOT_WINDOWS
        var attempts = 0;
        using var cache = new ItemRulesTranslations(() => ++attempts switch
        {
            1 => [],
            2 => throw new IOException("Synthetic failure"),
            _ => Entries("+# to Armour")
        });
        Assert.Empty(await cache.LoadAsync());
        await Assert.ThrowsAsync<IOException>(() => cache.LoadAsync());
        var successful = cache.LoadAsync();
        Assert.Equal(new[] { "+# to Armour" }, await successful);
        Assert.Same(successful, cache.LoadAsync());
        Assert.Equal(3, attempts);
#else
        await Task.CompletedTask;
#endif
    }
    [WindowsOnlyFact]
    public async Task StatsSignalInvalidatesCacheAndNotifiesOpenConsumers()
    {
#if SNAPSHOT_WINDOWS
        var packs = new TestPacks();
        var template = "+# to Armour";
        using var cache = new ItemRulesTranslations(() => Entries(template), packs);
        var notifications = 0;
        cache.Changed += () => notifications++;
        var first = cache.LoadAsync();
        await first;
        packs.Publish("poe1/bases");
        Assert.Same(first, cache.LoadAsync());
        template = "+# to maximum Life";
        packs.Publish("poe1/stats");
        Assert.Equal(1, notifications);
        Assert.Equal(new[] { template }, await cache.LoadAsync());
        cache.Dispose();
        packs.Publish("poe1/stats");
        Assert.Equal(1, notifications);
#else
        await Task.CompletedTask;
#endif
    }
    [WindowsOnlyFact]
    public void CompletedSearchAndSavedNoticeSurvivePreviewRefresh()
    {
#if SNAPSHOT_WINDOWS
        WithStore(store => SnapshotRenderer.Render(() =>
        {
            var pending = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var source = new ItemInfoViewModel();
            var host = SettingsPageTestTheme.Apply(new ItemRulesPageHost(store, source, pending.Task));
            T Find<T>(string name) where T : FrameworkElement => Descendants(host.Content).OfType<T>().Single(e => e.Name == name);
            void Click(string tag) => ClickButton(host, tag);
            Assert.Equal("Loading mod search…", Find<TextBlock>("SearchStatus").Text);
            Assert.False(Find<TextBox>("RuleSearch").IsEnabled);
            // The UI has not pumped the load continuation. Search must use the completed task itself.
            pending.SetResult(["+# to Armour"]);
            Find<TextBox>("RuleSearch").Text = "Armour";
            Assert.True(Find<TextBox>("RuleSearch").IsEnabled);
            Assert.Equal("", Find<TextBlock>("SearchStatus").Text);
            Assert.Single(Find<ListBox>("RuleResults").Items);
            Click("SaveRules");
            host.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            source.Close();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Equal("Changes saved.", Find<TextBlock>("RulesNotice").Text);
            Click("Profile1");
            Assert.Equal("Changes saved.", Find<TextBlock>("RulesNotice").Text);
            Find<CheckBox>("Advanced").IsChecked = false;
            Assert.Equal("", Find<TextBlock>("RulesNotice").Text);
            host.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            return host;
        }, "item-rules-search-notice", new Size(1280, 1200)));
#endif
    }
    [WindowsOnlyFact]
    public void OpenPageRefreshesSearchWhenStatsPackChanges()
    {
#if SNAPSHOT_WINDOWS
        WithStore(store => SnapshotRenderer.Render(() =>
        {
            var packs = new TestPacks();
            var text = "+# to Armour";
            using var cache = new ItemRulesTranslations(() => Entries(text), packs);
            cache.LoadAsync().GetAwaiter().GetResult();
            var host = SettingsPageTestTheme.Apply(new ItemRulesPageHost(store, null, cache));
            host.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            var search = Descendants(host.Content).OfType<TextBox>().Single(t => t.Name == "RuleSearch");
            var results = Descendants(host.Content).OfType<ListBox>().Single();
            search.Text = "Armour";
            Assert.Single(results.Items);
            text = "+# to maximum Life";
            packs.Publish("poe1/stats");
            cache.LoadAsync().GetAwaiter().GetResult();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Empty(results.Items);
            search.Text = "Life";
            Assert.Single(results.Items);
            host.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            return host;
        }, "item-rules-live-pack-refresh", new Size(1280, 1200)));
#endif
    }
    [WindowsOnlyFact]
    public void HiddenInvalidProfileIsNamedInSaveNotice()
    {
#if SNAPSHOT_WINDOWS
        WithStore(store => SnapshotRenderer.Render(() =>
        {
            store.Update("iteminfo", d => d.Features.Sections["iteminfo"] = new()
            {
                ["profiles"] = JsonSerializer.SerializeToElement(new ItemInfoSettings([
                    new("Caster", [new("+0 to Armour", Highlight.Desired, "Shields")]), new("Other", [])]))
            });
            var host = SettingsPageTestTheme.Apply(new ItemRulesPageHost(store, null, []));
            Descendants(host.Content).OfType<TextBox>().Single(t => t.Name == "Classes").Text = "Body Armour";
            ClickButton(host, "Profile1"); ClickButton(host, "SaveRules");
            var notice = Descendants(host.Content).OfType<TextBlock>().Single(t => t.Name == "RulesNotice").Text;
            Assert.Contains("Profile 1 (Caster)", notice);
            Assert.Contains("Body Armour", notice);
            return host;
        }, "item-rules-hidden-invalid-profile", new Size(1280, 1200)));
#endif
    }
#if SNAPSHOT_WINDOWS
    private static StatTranslation[] Entries(string text) => [new("synthetic", ["armour"], [new(text, [], [], [])])];
    private sealed class TestPacks : IPackReader
    {
        public PackSnapshot<T>? TryGet<T>(string id) where T : class => null;
        public event Action<PackChange>? Changed;
        public void Publish(string id) => Changed?.Invoke(new(id, "test", "synthetic"));
    }
    private static void WithStore(Action<JsonSettingsStore<SettingsDocument>> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "item-rules-polish-" + Guid.NewGuid());
        try
        {
            using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(directory), Path.Combine(directory, "settings.json"),
                SettingsJsonContext.Default.SettingsDocument, () => new());
            action(store);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    private static void ClickButton(ContentControl host, string tag)
    {
        var button = Descendants(host.Content).OfType<Button>().Single(b => Equals(b.Tag, tag));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    }
    private static IEnumerable<object> Descendants(object root)
    {
        yield return root;
        if (root is DependencyObject d)
            foreach (var child in LogicalTreeHelper.GetChildren(d))
                foreach (var nested in Descendants(child)) yield return nested;
    }
#endif
}
