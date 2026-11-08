using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExileSight.App.Features.Guide;
using ExileSight.App.Ui;
using ExileSight.App.Shell;
using ExileSight.Core.DataPacks;
using ExileSight.Features.Guide;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class GuidePageSnapshotTests
{
    [WindowsOnlyFact]
    public void ManagementPage()
    {
#if SNAPSHOT_WINDOWS
        var path = SnapshotRenderer.Render(() =>
        {
            var theme = CardTheme.Default;
            var snapshot = new GuideSnapshot(true, 2, 3, 9, TimeSpan.FromMinutes(7),
                TimeSpan.FromMinutes(30), 1d / 3, ["Travel to the forest", "Find the waypoint"],
                "Return to town", "Progress and timers reset.", false) { Bandit = "oak" };
            var page = GuidePageBuilder.Build(new(snapshot), theme);
            page.Measure(new Size(900, 760)); page.Arrange(new Rect(0, 0, 900, 760));
            var buttons = Descendants(page).OfType<Button>().ToArray();
            Assert.Equal(Enum.GetValues<GuidePageAction>().Order(),
                buttons.Select(b => Assert.IsType<GuidePageAction>(b.Tag)).Order());
            Assert.All(buttons, button => Assert.True(button.IsEnabled));
            return new Border { Background = theme.AppBackground, Padding = new Thickness(24), Child = page };
        }, "guide-management-page", new Size(948, 808));
        Assert.True(File.Exists(path));
        using var stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = Assert.Single(decoder.Frames);
        Assert.Equal(948, frame.PixelWidth); Assert.Equal(808, frame.PixelHeight);
#endif
    }

    [WindowsOnlyFact]
    public void ShellDetectsProgressAndRebuiltHostRetainsOperationFailure()
    {
#if SNAPSHOT_WINDOWS
        var path = SnapshotRenderer.Render(() =>
        {
            var model = new GuideModel();
            model.Load(new GuideData([new(1, 1, new("league-start", ["yes"]),
                [new("Find the waypoint", [], [], [])])], [], []), "fixture");
            var snapshot = model.StructuredSnapshot();
            var shell = new ShellModel("Act guide", "Standard", [], [], null) { Guide = snapshot };
            Assert.True(shell.SameContent(shell with { Guide = model.StructuredSnapshot() }));
            Assert.False(shell.SameContent(shell with { Guide = snapshot with { Step = 2 } }));

            var state = new GuidePageState();
            var host = new GuidePageHost(model, state);
            host.Measure(new Size(900, 760)); host.Arrange(new Rect(0, 0, 900, 760));
            var toggle = Descendants(host).OfType<Button>()
                .Single(b => b.Tag is GuidePageAction.LeagueStart);
            // This fixture has no non-league-start page: the real operation must fail safely.
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggle));
            Assert.Equal("Guide operation failed: ArgumentException", state.Status);
            var rebuilt = new GuidePageHost(model, state);
            rebuilt.Measure(new Size(900, 760)); rebuilt.Arrange(new Rect(0, 0, 900, 760));
            Assert.Contains(Descendants(rebuilt).OfType<TextBlock>(), text => text.Text == state.Status);
            Assert.True(model.StructuredSnapshot().LeagueStart);
            return rebuilt;
        }, "guide-page-retained-status", new Size(900, 760));
        Assert.True(File.Exists(path));
#endif
    }

#if SNAPSHOT_WINDOWS
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
#endif
}
