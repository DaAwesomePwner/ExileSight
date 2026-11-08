using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExileSight.App.Features.Guide;
using ExileSight.App.Ui;
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
