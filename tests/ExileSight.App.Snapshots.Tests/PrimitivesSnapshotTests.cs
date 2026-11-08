using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExileSight.App.Ui;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class PrimitivesSnapshotTests
{
    [WindowsOnlyFact]
    public void Pills()
    {
#if SNAPSHOT_WINDOWS
        foreach (var tone in Enum.GetValues<CardTone>())
            Render("pill-tone-" + tone, t => CardPrimitives.TierPill(new(PillKind.Tier, 7, tone), t));
        foreach (var kind in Enum.GetValues<PillKind>().Where(k => k != PillKind.Tier))
            Render("pill-kind-" + kind, t => CardPrimitives.TierPill(new(kind), t));
#endif
    }

    [WindowsOnlyFact]
    public void Rolls()
    {
#if SNAPSHOT_WINDOWS
        foreach (var position in new double?[] { 0, .5, 1, null })
            Render("roll-" + (position?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"), t =>
            {
                var model = new RollBarModel(position, position == 1 ? CardTone.Best :
                    position == 0 ? CardTone.Low : CardTone.Medium);
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                var bar = CardPrimitives.RollBar(model, t);
                Assert.Equal(40, bar.Width);
                Assert.Equal(6, bar.Height);
                if (position.HasValue)
                    Assert.Equal(40 * position.Value, ((FrameworkElement)((Grid)bar).Children[1]).Width);
                row.Children.Add(bar);
                var label = CardPrimitives.RollLabel(model, t);
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
            Render("pips-" + state, t => CardPrimitives.SlotPips([state, state, state], t));
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
                Assert.DoesNotContain("-", delta.Text);
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
            Render($"verdict-{model.Wanted}-{model.Unwanted}", t => CardPrimitives.VerdictBand(model, t));
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

