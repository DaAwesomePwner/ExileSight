using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExileSight.App.Features.MapInfo;
using ExileSight.App.Ui;
using ExileSight.Features.MapInfo;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class MapCardSnapshotTests
{
    [WindowsOnlyFact]
    public void FirstTime()
    {
#if SNAPSHOT_WINDOWS
        Render("first-time", new("Jungle Valley Map", 64, 31, 18,
            [new("1", "Extra lightning dmg", "Monsters"), new("2", "Hexproof", "Monsters"),
             new("3", "Less recovery", "Players"), new("4", "Increased life", "Unique boss"),
             new("5", "Shocked ground", "Area")], 14, "rare"));
#endif
    }

    [WindowsOnlyFact]
    public void RatedAndFolded()
    {
#if SNAPSHOT_WINDOWS
        Render("rated", new("Strand Map", 82, 46, 28,
            [new("1", "Reflect 18% elemental", "Monsters", 3), new("2", "Cannot regenerate", "Players", 3),
             new(null, "Unknown", null), new("3", "Crit chance + multi", "Monsters", 2),
             new("4", "−12% max resists", "Players", 1), new("5", "Burning ground", "Area", 1),
             new("6", "Hexproof", "Monsters"), new("7", "Hidden zero", "Area", 0)], 16, "rare", true));
#endif
    }

    [WindowsOnlyFact]
    public void UnknownStats()
    {
#if SNAPSHOT_WINDOWS
        Render("unknown-stats", new("Map", null, null, null, [new(null, "Unknown", null)]));
#endif
    }

#if SNAPSHOT_WINDOWS
    private static void Render(string name, MapResult result)
    {
        var path = SnapshotRenderer.Render(() =>
        {
            var card = MapCardBuilder.Build(new(result, new()), CardTheme.Default);
            Assert.Equal(300, card.Width);
            card.Measure(new Size(300, double.PositiveInfinity));
            card.Arrange(new Rect(0, 0, 300, card.DesiredSize.Height));
            card.UpdateLayout();
            var texts = Descendants(card).OfType<TextBlock>().Select(t => t.Text).ToArray();
            Assert.Contains("Click a mod to cycle 0–3 · rating saved for every map", texts);
            if (name == "first-time") Assert.Contains("Rate each mod once by clicking it. Every later map sorts itself.", texts);
            if (name == "unknown-stats") Assert.Equal(4, texts.Count(t => t == "?"));
            if (name == "rated")
            {
                var reveal = Descendants(card).OfType<Button>().Single(b => b.Content is TextBlock t && t.Text.StartsWith("+1 more"));
                reveal.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(Visibility.Collapsed, reveal.Visibility);
                // Restore the folded state for the deterministic snapshot.
                card = MapCardBuilder.Build(new(result, new()), CardTheme.Default);
            }
            return card;
        }, "map-card-" + name);
        Assert.True(File.Exists(path));
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
#endif
}
