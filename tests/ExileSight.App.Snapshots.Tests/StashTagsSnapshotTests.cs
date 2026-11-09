using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Features.Stashninja;
using ExileSight.App.Ui;
using ExileSight.Features.Stashninja;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class StashTagsSnapshotTests
{
    [WindowsOnlyFact]
    public void ThresholdTags()
    {
#if SNAPSHOT_WINDOWS
        var path = SnapshotRenderer.Render(() =>
        {
            var theme = CardTheme.Default;
            var panel = new StackPanel { Background = theme.OverlayBackground };
            foreach (var colors in new[] { ("#6AA8FF", "#6FE3D5"), ("#101820", "#FFFFFF") })
            {
                // Synthetic tags cover both profile slots and both text formats.
                foreach (var tag in new[] { Fixture()[0] with { Text = "45", Color = colors.Item1 },
                    Fixture()[1] with { Text = "1.4d", Color = colors.Item2 } })
                {
                    var visual = StashTagBuilder.Tag(tag, theme);
                    visual.HorizontalAlignment = HorizontalAlignment.Left;
                    visual.Margin = new Thickness(8);
                    Assert.Equal(14, visual.Height);
                    Assert.Equal(10, ((TextBlock)visual.Child).FontSize);
                    panel.Children.Add(visual);
                }
            }
            return panel;
        }, "stash-tags", new Size(280, 140));
        Assert.True(File.Exists(path));
#endif
    }

    [WindowsOnlyFact] public void ThreeHighest() => Summary(3, "14 min ago");
    [WindowsOnlyFact] public void OneHighest() => Summary(1, "14 min ago");
    [WindowsOnlyFact] public void StalePrices() => Summary(3, "2 d ago · stale");

    [WindowsOnlyFact]
    public void PositionedTagsAndSummaryMatchOcclusionBounds()
    {
#if SNAPSHOT_WINDOWS
        Assert.True(File.Exists(SnapshotRenderer.Render(() =>
        {
            var theme = CardTheme.Default;
            var tags = Fixture();
            var layout = new Layout("currency", "general", new(900, 400), tags.Select(t => t.Cell).ToArray(),
                [], new("synthetic", "synthetic", new(2026, 1, 1)));
            var visuals = StashTagBuilder.Build(layout, tags, new(1.5, 20, 30), new(900, 400),
                1.5, "14 min ago", theme);
            Assert.Equal(4, visuals.Count);
            var canvas = new Canvas { Width = 600, Height = 267, Background = theme.OverlayBackground };
            foreach (var visual in visuals)
            {
                var bounds = visual.Bounds;
                visual.Element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.Equal(bounds.Width / 1.5, visual.Element.DesiredSize.Width, 5);
                Assert.Equal(bounds.Height / 1.5, visual.Element.DesiredSize.Height, 5);
                Canvas.SetLeft(visual.Element, bounds.X / 1.5);
                Canvas.SetTop(visual.Element, bounds.Y / 1.5);
                canvas.Children.Add(visual.Element);
            }
            Assert.True(visuals[^1].Bounds.X > visuals.Take(3).Max(v => v.Bounds.X + v.Bounds.Width));
            return canvas;
        }, "stash-positioned")));
#endif
    }

    private static void Summary(int count, string age)
    {
#if SNAPSHOT_WINDOWS
        Assert.True(File.Exists(SnapshotRenderer.Render(() =>
            StashTagBuilder.Summary(Fixture().Take(count), age, CardTheme.Default),
            $"stash-summary-{count}-{(age.Contains("stale") ? "stale" : "fresh")}")));
#endif
    }

#if SNAPSHOT_WINDOWS
    private static PriceTag[] Fixture() => new[] { ("Divine Orb", 2400m, "12d"),
        ("Mirror Shard", 620m, "3.1d"), ("Exalted Orb", 280m, "1.4d") }.Select((item, i) => new PriceTag(
            new(item.Item1, i * 50, 0, 44, 44, "Currency", ItemName: item.Item1), item.Item1,
            item.Item2, item.Item3, "#6FE3D5", [100, 103, 101, 108, 110, 114, 118])).ToArray();
#endif
}
