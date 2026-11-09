using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ExileSight.App.Features.Lab;
using ExileSight.App.Features.Macros;
using ExileSight.App.Ui;
using ExileSight.Features.Lab;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class LabSnapshotTests
{
    [WindowsOnlyFact]
    public void NoLayoutImported()
    {
#if SNAPSHOT_WINDOWS
        Render("lab-empty", null, null, "", "No saved layout");
#endif
    }

    [WindowsOnlyFact]
    public void CurrentRoomHighlighted()
    {
#if SNAPSHOT_WINDOWS
        Render("lab-current-room", Layout(new(2026, 11, 9)), "2", "", "Tracking: Trial chamber");
#endif
    }

    [WindowsOnlyFact]
    public void TwoDigitCurrentRoomFits()
    {
#if SNAPSHOT_WINDOWS
        Render("lab-current-room-two-digits", Layout(new(2026, 11, 9), "12"), "12", "", "Tracking: Trial chamber");
#endif
    }

    [WindowsOnlyFact]
    public void OutdatedLayoutWarning()
    {
#if SNAPSHOT_WINDOWS
        Render("lab-outdated", Layout(new(2026, 11, 8)), "2", "", "⚠ OUTDATED");
#endif
    }

    [WindowsOnlyFact]
    public void ImportErrorShown()
    {
#if SNAPSHOT_WINDOWS
        Render("lab-error", null, null, "Lab JSON exceeds the 1 MB limit.", "⚠ Lab JSON exceeds");
#endif
    }

#if SNAPSHOT_WINDOWS
    // Synthetic rooms only: deliberately includes every map symbol, no upstream JSON.
    private static LabLayout Layout(DateOnly date, string currentId = "2") => new("Normal", date, "Sword", "Charges", "Idols",
        "Spikes", "Blades", [],
        [new("1", "Entrance", "a", 0, 0, ["darkshrine", "golden-key"], new Dictionary<string, string> { ["E"] = currentId }, null),
         new(currentId, "Trial chamber", "b", 1, 1, ["golden-door", "argus", "trap"], new Dictionary<string, string>(), null)]);

    private static void Render(string name, LabLayout? layout, string? room, string error, string expected)
    {
        var path = SnapshotRenderer.Render(() =>
        {
            var theme = CardTheme.Default;
            var card = LabCardBuilder.Build((layout, room, room is null ? "Not tracking" : "Tracking: Trial chamber", "Normal"),
                error, "Complete the trials before entering the Labyrinth.",
                new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero), theme);
            Assert.Same(theme.OverlayBackground, card.Background);
            Assert.Equal(new CornerRadius(6), card.CornerRadius);
            Assert.Equal(new[] { "Open poelab", "Import file…", "Paste JSON", "Close" },
                OnDemandControls.Buttons(card).Select(b => (string)b.Tag));
            Assert.Contains(Descendants(card).OfType<TextBlock>(), t => t.Text.Contains(expected, StringComparison.Ordinal));
            Assert.Equal("Normal", Assert.IsType<ComboBox>(card.FindName("Difficulty")).SelectedItem);
            if (room is not null)
            {
                var map = Assert.Single(Descendants(card).OfType<Canvas>());
                foreach (var marker in map.Children.OfType<Border>())
                {
                    var label = Assert.IsType<TextBlock>(marker.Child);
                    Assert.Equal(HorizontalAlignment.Center, label.HorizontalAlignment);
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Assert.True(label.DesiredSize.Width <= marker.Width - marker.BorderThickness.Left - marker.BorderThickness.Right);
                }
                var current = Assert.Single(map.Children.OfType<Border>(), b => ((TextBlock)b.Child).Text == room);
                Assert.Same(theme.OverlayAccent, current.Background);
                Assert.Equal(new Thickness(3), current.BorderThickness);
                Assert.Contains(Descendants(card).OfType<TextBlock>(), t =>
                    t.Text.StartsWith("◎ Tracking:", StringComparison.Ordinal) && t.Foreground == theme.OverlayAccent);
            }
            return card;
        }, name);
        Assert.True(File.Exists(path));
        using var stream = File.OpenRead(path);
        var frame = Assert.Single(new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames);
        Assert.Equal(444, frame.PixelWidth);
        Assert.True(frame.PixelHeight > 200);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
#endif
}
