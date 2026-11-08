using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ExileSight.App.Features.Vaalasync;
using ExileSight.App.Ui;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class ShopPricerSnapshotTests
{
    [WindowsOnlyFact]
    public void ArmedAsync()
    {
#if SNAPSHOT_WINDOWS
        Render("armed", Fixture());
#endif
    }

    [WindowsOnlyFact]
    public void DuplicateDisabled()
    {
#if SNAPSHOT_WINDOWS
        var data = Fixture() with { ListedNow = "10 chaos", Currency = "chaos", Amounts = [
            new("minus10", "−10%", "9", false), new("minus1", "−1", "9", true),
            new("plus1", "+1", "11", true), new("plus10", "+10%", "11", false)] };
        Render("duplicate", data);
#endif
    }

    [WindowsOnlyFact]
    public void Disarmed()
    {
#if SNAPSHOT_WINDOWS
        Render("disarmed", Fixture() with { Armed = false, Notice = "Price possibly set — press omni again before repricing." });
#endif
    }

    [WindowsOnlyFact]
    public void Pasted()
    {
#if SNAPSHOT_WINDOWS
        Render("pasted", Fixture() with { Armed = false, Pasted = "39" });
#endif
    }

    [WindowsOnlyFact]
    public void NoteMode()
    {
#if SNAPSHOT_WINDOWS
        var data = Fixture();
        Render("note", data with { NoteMode = true,
            Amounts = data.Amounts.Select(a => a with { Text = "~b/o " + a.Text + " divine" }).ToArray() });
#endif
    }

    [WindowsOnlyFact]
    public void NoTimestamp()
    {
#if SNAPSHOT_WINDOWS
        Render("no-timestamp", Fixture() with { ListedFor = null });
#endif
    }

#if SNAPSHOT_WINDOWS
    private static ShopCardModel Fixture() => new("Astral Plate", "40 divine", TimeSpan.FromHours(29),
        "divine", true, false, [new("minus10", "−10%", "36", true), new("minus1", "−1", "39", true),
            new("plus1", "+1", "41", true), new("plus10", "+10%", "44", true)]);

    private static void Render(string name, ShopCardModel model)
    {
        var path = SnapshotRenderer.Render(() =>
        {
            var card = ShopCardBuilder.Build(model, CardTheme.Default);
            Assert.Equal(330, card.Width);
            var descendants = Descendants(card).ToArray();
            var buttons = descendants.OfType<Button>().Where(b => model.Amounts.Any(a => a.Choice == (string)b.Tag)).ToArray();
            Assert.Equal(4, buttons.Length);
            foreach (var button in buttons)
            {
                Assert.Equal(56, button.Height);
                Assert.Equal(model.Armed && model.Amounts.Single(a => a.Choice == (string)button.Tag).Available, button.IsEnabled);
            }
            Assert.Equal(model.ListedFor is not null && model.Pasted is null,
                descendants.OfType<TextBlock>().Any(t => t.Text == "Listed for"));
            if (model.Pasted is not null)
                Assert.Contains(descendants.OfType<TextBlock>(), t => t.Text == "Pasted " + model.Pasted);
            return card;
        }, "shop-" + name);
        Assert.True(File.Exists(path));
        using var stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.Equal(330, Assert.Single(decoder.Frames).PixelWidth);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
#endif
}
