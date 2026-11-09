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
    public void PositionedTagsAndSummaryMatchOcclusionBounds() => Positioned(1920, 1080);

    [WindowsOnlyFact]
    public void PositionedTagsAt720p() => Positioned(1280, 720);

    private static void Positioned(int width, int height)
    {
#if SNAPSHOT_WINDOWS
        Assert.True(File.Exists(SnapshotRenderer.Render(() =>
        {
            var theme = CardTheme.Default;
            // Read shipped measurements; prices and positions remain synthetic.
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "data/stash-layouts/poe1")))
                directory = directory.Parent;
            Assert.NotNull(directory);
            var smallest = Directory.GetFiles(Path.Combine(directory.FullName, "data/stash-layouts/poe1"), "*.layout.json")
                .Select(path => Layouts.Parse(File.ReadAllText(path)))
                .SelectMany(l => l.Cells.Select(c => (Layout: l, Cell: c,
                    Area: c.W * c.H * Math.Pow(1080d / l.ReferenceResolution.Height, 2))))
                .OrderBy(pair => pair.Area).First();
            Assert.Equal(36, smallest.Cell.W);
            Assert.Equal(35, smallest.Cell.H);
            var tags = Fixture().Select((t, i) => t with
            {
                Cell = smallest.Cell with { Id = $"synthetic-{i}", X = 20 + i * 50, Y = 20, Folder = null },
                Text = new[] { "3.1d", "999", "1234" }[i]
            }).ToArray();
            var layout = smallest.Layout with { Cells = tags.Select(t => t.Cell).ToArray() };
            var resolution = new Resolution(width, height);
            var transform = ExileSight.Features.Stashninja.Geometry.Fit(layout.ReferenceResolution, resolution);
            var visuals = StashTagBuilder.Build(layout, tags, transform, resolution, 1, "14 min ago", theme);
            Assert.Equal(4, visuals.Count);
            var canvas = new Canvas { Width = width, Height = height, Background = theme.OverlayBackground };
            foreach (var visual in visuals)
            {
                var bounds = visual.Bounds;
                visual.Element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.Equal(bounds.Width, visual.Element.DesiredSize.Width, 5);
                Assert.Equal(bounds.Height, visual.Element.DesiredSize.Height, 5);
                Canvas.SetLeft(visual.Element, bounds.X); Canvas.SetTop(visual.Element, bounds.Y);
                canvas.Children.Add(visual.Element);
            }
            canvas.Measure(new Size(width, height));
            canvas.Arrange(new Rect(0, 0, width, height));
            canvas.UpdateLayout();
            for (var i = 0; i < tags.Length; i++)
            {
                var border = Assert.IsType<Border>(visuals[i].Element);
                var text = Assert.IsType<TextBlock>(border.Child);
                Assert.Equal(TextTrimming.None, text.TextTrimming);
                Assert.Equal(tags[i].Text, text.Text); // Real smallest cells fit the full examples.
                var probe = CardPrimitives.Text(text.Text, theme, size: 10, number: true);
                probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.True(text.ActualWidth >= probe.DesiredSize.Width);
            }
            Assert.True(visuals[^1].Bounds.X > visuals.Take(3).Max(v => v.Bounds.X + v.Bounds.Width));
            return canvas;
        }, $"stash-positioned-{height}")));
#endif
    }

    [WindowsOnlyFact]
    public void NarrowTagsCompactOrDisappearWithoutOcclusionBounds()
    {
#if SNAPSHOT_WINDOWS
        SnapshotRenderer.Render(() =>
        {
            var tags = Fixture().Take(2).Select((t, i) => t with
            { Text = "3.1d", Cell = t.Cell with { W = i == 0 ? 24 : 5 } }).ToArray();
            var layout = new Layout("synthetic", "general", new(1920, 1080), tags.Select(t => t.Cell).ToArray(),
                [], new("synthetic", "synthetic", new(2026, 1, 1)));
            var visuals = StashTagBuilder.Build(layout, tags, new(1, 0, 0), new(1920, 1080), 1,
                "14 min ago", CardTheme.Default);
            Assert.Equal(2, visuals.Count); // One compact tag plus summary; omitted tag has no bounds.
            var tag = Assert.IsType<Border>(visuals[0].Element);
            Assert.Equal("3d", Assert.IsType<TextBlock>(tag.Child).Text);
            return tag;
        }, "stash-compact");
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
