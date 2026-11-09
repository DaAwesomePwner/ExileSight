using Xunit;
#if SNAPSHOT_WINDOWS
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ExileSight.App.Ui;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class AlignedPrimitivesSnapshotTests
{
    [WindowsOnlyFact]
    public void UniqueBands()
    {
#if SNAPSHOT_WINDOWS
        foreach (var (name, value, expected, fraction) in new (string, double?, string, double?)[]
        {
            ("86", .86, "86%", .86), ("0", 0, "0%", 0), ("clamp-high", 120, "100%", 1),
            ("clamp-low", -.2, "0%", 0), ("unknown", null, "—", null),
            ("NaN", double.NaN, "—", null), ("infinity", double.PositiveInfinity, "100%", 1),
            ("negative-infinity", double.NegativeInfinity, "0%", 0)
        })
        {
            Render("verdict-unique-" + name, 84, theme =>
            {
                var previous = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                    var band = CardPrimitives.VerdictBand(new UniqueVerdictModel(value), theme);
                    Assert.Equal(Visibility.Visible, band.Visibility);
                    Assert.Same(theme.OverlayRaised, band.Background);
                    Assert.Equal(new CornerRadius(4), band.CornerRadius);
                    var row = (Grid)band.Child;
                    Assert.Equal("Average roll", ((TextBlock)row.Children[0]).Text);
                    var bar = (Grid)row.Children[1];
                    var number = (TextBlock)row.Children[2];
                    Assert.Equal(expected, number.Text);
                    Assert.Equal(CardPrimitives.RollLabel(new(value, CardTone.High), theme).Text, number.Text);
                    Assert.Same(theme.NumberFont, number.FontFamily);
                    Assert.Equal(FontWeights.Bold, number.FontWeight);
                    Assert.Equal(HorizontalAlignment.Right, number.HorizontalAlignment);
                    foreach (var width in new[] { 274d, 340d, 274d })
                    {
                        band.Measure(new Size(width, 62));
                        band.Arrange(new Rect(0, 0, width, band.DesiredSize.Height));
                        band.UpdateLayout();
                        Assert.True(bar.ActualWidth > 100);
                        if (fraction.HasValue)
                        {
                            Assert.Equal(2, bar.Children.Count);
                            var fill = (Rectangle)bar.Children[1];
                            Assert.Equal(bar.ActualWidth * fraction.Value, fill.Width, 6);
                            Assert.Equal(fill.Width, fill.ActualWidth, 6);
                        }
                        else
                            Assert.NotEmpty(((Rectangle)Assert.Single(bar.Children.Cast<UIElement>())).StrokeDashArray);
                    }
                    return band;
                }
                finally { CultureInfo.CurrentCulture = previous; }
            });
        }
#endif
    }

    [WindowsOnlyFact]
    public void AlignedPillColumn()
    {
#if SNAPSHOT_WINDOWS
        Render("pills-aligned-column", 150, theme =>
        {
            var column = new StackPanel();
            foreach (var model in new TierPillModel[]
            {
                new(PillKind.Tier, 1, CardTone.Best), new(PillKind.Tier, 10, CardTone.Low),
                new(PillKind.Implicit), new(PillKind.Enchant), new(PillKind.Unknown)
            })
            {
                var pill = (Grid)CardPrimitives.TierPill(model, theme);
                pill.Measure(new Size(274, 18));
                pill.Arrange(new Rect(0, 0, pill.DesiredSize.Width, 18));
                var label = Assert.Single(pill.Children.OfType<TextBlock>());
                Assert.Equal(theme.TierPillWidth, pill.ActualWidth);
                Assert.True(pill.ActualWidth >= label.ActualWidth + 8);
                var origin = label.TranslatePoint(new Point(), pill);
                Assert.Equal((pill.ActualWidth - label.ActualWidth) / 2, origin.X, 5);
                pill.Margin = new Thickness(0, 0, 0, 6);
                column.Children.Add(pill);
            }
            return column;
        });
#endif
    }

#if SNAPSHOT_WINDOWS
    private static void Render(string name, int height, Func<CardTheme, FrameworkElement> build)
    {
        var path = SnapshotRenderer.Render(() => CardPrimitives.CardFrame(build(CardTheme.Default),
            CardTheme.Default), "primitive-" + name, new Size(300, height));
        Assert.True(File.Exists(path));
        using var stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = Assert.Single(decoder.Frames);
        Assert.Equal(300, frame.PixelWidth);
        Assert.Equal(height, frame.PixelHeight);
    }
#endif
}
