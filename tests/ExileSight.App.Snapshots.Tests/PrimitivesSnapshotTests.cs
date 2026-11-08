using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ExileSight.App.Ui;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class PrimitivesSnapshotTests
{
    [WindowsOnlyFact]
    public void Pills()
    {
#if SNAPSHOT_WINDOWS
        for (var tier = 1; tier <= 12; tier++)
            Render("pill-tier-" + tier, t => CheckPill(new(PillKind.Tier, tier, CardTone.High), t, "T" + tier));
        foreach (var tone in Enum.GetValues<CardTone>())
            Render("pill-tone-" + tone, t => CheckPill(new(PillKind.Tier, 7, tone), t, tone == CardTone.Unknown ? "T?" : "T7"));
        foreach (var kind in Enum.GetValues<PillKind>().Where(k => k != PillKind.Tier))
            Render("pill-kind-" + kind, t => CheckPill(new(kind), t, kind switch
            {
                PillKind.Implicit => "imp", PillKind.Crafted => "cr", PillKind.Unique => "U",
                PillKind.Enchant => "ench", _ => "T?"
            }));
#endif
    }

    [WindowsOnlyFact]
    public void Rolls()
    {
#if SNAPSHOT_WINDOWS
        foreach (var position in new double?[] { 0, .5, 1, null, -.5, 1.5, double.NaN, double.NegativeInfinity, double.PositiveInfinity })
            Render("roll-" + (position?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"), t =>
            {
                var model = new RollBarModel(position, position == 1 ? CardTone.Best :
                    position == 0 ? CardTone.Low : CardTone.Medium);
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                var bar = CardPrimitives.RollBar(model, t);
                Assert.Equal(40, bar.Width);
                Assert.Equal(6, bar.Height);
                if (position.HasValue && !double.IsNaN(position.Value))
                    Assert.Equal(40 * Math.Clamp(position.Value, 0, 1), ((FrameworkElement)((Grid)bar).Children[1]).Width);
                else
                {
                    Assert.Single(((Grid)bar).Children.Cast<UIElement>());
                    Assert.NotEmpty(((Rectangle)((Grid)bar).Children[0]).StrokeDashArray);
                }
                row.Children.Add(bar);
                var label = CardPrimitives.RollLabel(model, t);
                Assert.Equal(position.HasValue && !double.IsNaN(position.Value)
                    ? Math.Clamp(position.Value, 0, 1).ToString("0%", System.Globalization.CultureInfo.InvariantCulture)
                    : "—", label.Text);
                label.Margin = new Thickness(8, 0, 0, 0);
                row.Children.Add(label);
                return row;
            });
#endif
    }

    [WindowsOnlyFact]
    public void Pips()
    {
#if SNAPSHOT_WINDOWS
        foreach (var state in Enum.GetValues<SlotState>())
            Render("pips-" + state, t =>
            {
                var pips = (StackPanel)CardPrimitives.SlotPips([state, state, state], t);
                if (state == SlotState.Unknown)
                    foreach (Rectangle pip in pips.Children) Assert.NotEmpty(pip.StrokeDashArray);
                return pips;
            });
        Render("pips-mixed", t => CardPrimitives.SlotPips([SlotState.Used, SlotState.Used, SlotState.Open], t));
#endif
    }

    [WindowsOnlyFact]
    public void Deltas()
    {
#if SNAPSHOT_WINDOWS
        foreach (var value in new[] { 37.5, -19.2, 0 })
            Render("delta-" + (value > 0 ? "up" : value < 0 ? "down" : "zero"), t =>
            {
                var delta = CardPrimitives.Delta(value, t);
                Assert.Equal(value > 0 ? "▲ 37.5" : value < 0 ? "▼ 19.2" : "0", delta.Text);
                Assert.Equal(FontNumeralAlignment.Tabular, Typography.GetNumeralAlignment(delta));
                return delta;
            });
#endif
    }

    [WindowsOnlyFact]
    public void TilesAndBands()
    {
#if SNAPSHOT_WINDOWS
        Render("tile-known", t => CardPrimitives.StatTile(new("Quantity", "72%", CardTone.Best), t));
        Render("tile-unknown", t => CardPrimitives.StatTile(new("Quantity", null), t));
        foreach (var model in new[] { new VerdictModel(2, 1), new VerdictModel(2, 0), new VerdictModel(0, 1), new VerdictModel(0, 0) })
            Render($"verdict-{model.Wanted}-{model.Unwanted}", t =>
            {
                var band = CardPrimitives.VerdictBand(model, t);
                Assert.Equal(model.Wanted == 0 && model.Unwanted == 0 ? Visibility.Collapsed : Visibility.Visible,
                    band.Visibility);
                return band;
            });
        foreach (var average in new double?[] { 86, 0, null })
            Render("verdict-unique-" + (average?.ToString() ?? "unknown"), t =>
            {
                var band = CardPrimitives.VerdictBand(new UniqueVerdictModel(average), t);
                Assert.Equal(average.HasValue ? Visibility.Visible : Visibility.Collapsed, band.Visibility);
                Assert.Equal(average.HasValue ? $"Average roll {average:0}%" : "", ((TextBlock)band.Child).Text);
                return band;
            });
        Render("frame-hairline", t =>
        {
            var column = new StackPanel();
            column.Children.Add(CardPrimitives.Text("Card primitives", t));
            column.Children.Add(CardPrimitives.Hairline(t));
            column.Children.Add(CardPrimitives.Text("Secondary text", t, "TextSecondary"));
            return column;
        });
#endif
    }

#if SNAPSHOT_WINDOWS
    private static FrameworkElement CheckPill(TierPillModel model, CardTheme theme, string expected)
    {
        var pill = (Grid)CardPrimitives.TierPill(model, theme);
        var label = Assert.Single(pill.Children.OfType<TextBlock>());
        Assert.Equal(expected, label.Text);
        pill.Measure(new Size(double.PositiveInfinity, 18));
        pill.Arrange(new Rect(0, 0, pill.DesiredSize.Width, 18));
        Assert.True(pill.ActualWidth >= 26);
        Assert.True(pill.ActualWidth >= label.ActualWidth + 8);
        Assert.True(label.ActualWidth >= label.DesiredSize.Width - label.Margin.Left - label.Margin.Right);
        if (expected == "T?") Assert.NotEmpty(((Rectangle)pill.Children[0]).StrokeDashArray);
        return pill;
    }

    private static void Render(string name, Func<CardTheme, FrameworkElement> build)
    {
        var path = SnapshotRenderer.Render(() =>
        {
            Assert.Null(Application.Current);
            var theme = CardTheme.Default;
            Assert.True(theme["OverlayBackground"].IsFrozen);
            var frame = CardPrimitives.CardFrame(build(theme), theme);
            Assert.Equal(300, frame.Width);
            Assert.Equal(new Thickness(12, 10, 12, 10), frame.Padding);
            return frame;
        }, "primitive-" + name, new Size(300, 84));
        Assert.True(File.Exists(path));
        using var stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = Assert.Single(decoder.Frames);
        Assert.Equal(300, frame.PixelWidth);
        Assert.Equal(84, frame.PixelHeight);
    }
#endif
}

