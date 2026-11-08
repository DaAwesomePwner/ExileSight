using Xunit;
#if SNAPSHOT_WINDOWS
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExileSight.App.Shell;
using ExileSight.App.Shell.Pages;
using ExileSight.App.Ui;
using ExileSight.Core.Input;
using ExileSight.Core.Settings;
using ExileSight.Core.Status;
#endif
namespace ExileSight.App.Snapshots.Tests;

// Regression helpers owned by SettingsPagesSnapshotTests.
public sealed class SettingsPagesReviewTests
{
    [WindowsOnlyFact]
    public void NavigationUsesDisplayedPageAndExplicitLeagueIdentity()
    {
#if SNAPSHOT_WINDOWS
        SnapshotRenderer.Render(() =>
        {
            var overlay = SettingsPageTestTheme.Apply(OverlayPageBuilder.Build(new(new(), new()), CardTheme.Default));
            // Target Home while the displayed tree still contains Density/Contrast.
            Assert.Null(ShellViewState.LeagueBox(overlay, "Overlay"));
            Assert.Null(ShellViewState.LeagueBox(overlay, "Home"));
            Assert.True(ShellRefreshPolicy.ShouldRebuild(true, false, false,
                ShellViewState.LeagueBox(overlay, "Overlay")?.Text, "Standard"));
            var home = SettingsPageTestTheme.Apply(HomeBuilder.Build(new("Home", "Standard", [], [], null), CardTheme.Default, false));
            var league = Assert.IsType<ComboBox>(ShellViewState.LeagueBox(home, "Home"));
            league.Text = "Unsaved league";
            Assert.False(ShellRefreshPolicy.ShouldRebuild(true, false, false, league.Text, "Standard"));
            league.Text = "Standard";
            Assert.True(ShellRefreshPolicy.ShouldRebuild(true, false, false, league.Text, "Standard"));
            return home;
        }, "settings-navigation-regression", new Size(1000, 900));
#endif
    }
    [WindowsOnlyFact]
    public void DropdownCaptureIsNotAnEditAndDragMustMove()
    {
#if SNAPSHOT_WINDOWS
        var directory = Path.Combine(Path.GetTempPath(), "settings-dirty-" + Guid.NewGuid());
        try
        {
            using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(directory), Path.Combine(directory, "settings.json"),
                SettingsJsonContext.Default.SettingsDocument, () => new());
            SnapshotRenderer.Render(() =>
            {
                var host = SettingsPageTestTheme.Apply(new OverlayPageHost(store, 16d / 9));
                T Find<T>(string name) where T : FrameworkElement => Descendants(host.Content).OfType<T>().Single(e => e.Name == name);
                Assert.False(Find<TextBox>("Scale").IsEnabled);
                var density = Find<ComboBox>("Density");
                density.IsDropDownOpen = true;
                host.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.PreviewMouseMoveEvent });
                density.IsDropDownOpen = false;
                Assert.False(host.Dirty);
                var canvas = Find<Canvas>("DockPreview");
                var original = (Point)canvas.Tag;
                OverlayPreview.CompleteDrag(canvas, original);
                Assert.False(host.Dirty);
                canvas.Tag = new Point(.2, .3);
                OverlayPreview.CompleteDrag(canvas, original);
                Assert.True(host.Dirty);
                var discard = Descendants(host.Content).OfType<Button>().Single(b => Equals(b.Tag, "DiscardOverlay"));
                discard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, discard));
                Assert.False(host.Dirty);
                Find<ComboBox>("Contrast").SelectedIndex = 1;
                Assert.True(host.Dirty);
                return host;
            }, "settings-dirty-regression", new Size(1000, 1000));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
#endif
    }
    [WindowsOnlyFact]
    public void TransparencyFormatsLoggedValuesAndExplainsUnavailableLog()
    {
#if SNAPSHOT_WINDOWS
        SnapshotRenderer.Render(() =>
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                var timestamp = new DateTimeOffset(2026, 1, 2, 12, 34, 56, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 1, 2)));
                Assert.Equal("12:34:56", TransparencyPageBuilder.FormatTime(timestamp, new DateTime(2026, 1, 2)));
                Assert.Equal("2026-01-02 12:34:56", TransparencyPageBuilder.FormatTime(timestamp, new DateTime(2026, 1, 3)));
                Assert.Equal("Copy item", TransparencyPageBuilder.ActionName("CopyItem"));
                Assert.Equal("FutureAction", TransparencyPageBuilder.ActionName("FutureAction"));
                var page = SettingsPageTestTheme.Apply(TransparencyPageBuilder.Build(new([], 0, false, "actions.log does not exist yet.", true), CardTheme.Default));
                var button = Assert.Single(Descendants(page).OfType<Button>());
                Assert.Equal("Open log folder", button.Content);
                Assert.Equal(HorizontalAlignment.Left, button.HorizontalAlignment);
                Assert.Contains(Descendants(page).OfType<TextBlock>(), t => t.Text == "actions.log does not exist yet.");
                return page;
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }, "settings-missing-log", new Size(1000, 800));
#endif
    }
#if SNAPSHOT_WINDOWS
    private static IEnumerable<object> Descendants(object root)
    {
        yield return root;
        if (root is DependencyObject dependency)
            foreach (var child in LogicalTreeHelper.GetChildren(dependency))
                foreach (var nested in Descendants(child)) yield return nested;
    }
#endif
}
