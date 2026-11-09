using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Features.Stashninja;
using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class RegressionTests
{
    private static Layout Fixture() => Layouts.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "fixtures", "overlapping.json")));

    [Fact] public void SameColumnCalibrationSharesClickErrorWithoutRelaxingTolerance()
    {
        var t = Geometry.Calibrate(new(100,100), new(100,300), new(170,172), new(174,516));
        Assert.Equal(1.72, t.Scale, 8);
        Assert.Equal(0, t.X, 8);
        Assert.Equal(0, t.Y, 8);
        Assert.Throws<ArgumentException>(() => Geometry.Calibrate(new(100,100), new(100,300), new(170,172), new(175,516)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.72)]
    public void TagBoundsAreCenteredInsetAndDoNotOverlap(double scale)
    {
        var l = Fixture(); var t = new Transform(scale, 12, 30);
        var a = Geometry.TagBounds(l, l.Cells[0], t);
        var b = Geometry.TagBounds(l, l.Cells[1], t);
        Assert.Equal(t.Apply(Geometry.Center(l,l.Cells[0])).X, a.X + a.Width / 2, 8);
        Assert.Equal(t.Apply(Geometry.Center(l,l.Cells[0])).Y, a.Y + a.Height / 2, 8);
        Assert.True(a.Width < l.Cells[0].W * scale);
        Assert.True(a.X + a.Width <= b.X);
    }

    [Fact] public void ItemMenuSelectsMatchingPageAndHotkeyRemembersLoadedPage()
    {
        var general = Fixture() with { TabId = "currency", Page = "general" };
        var essence = Fixture() with { TabId = "essence", Page = "general", Cells =
            [Fixture().Cells[0] with { ItemName = "Deafening Essence of Greed" }, Fixture().Cells[1]] };
        var model = new StashModel { Layouts = [essence, general] };
        model.Open(); Assert.Same(general, model.SelectedLayout);
        var item = ItemTextParser.Parse("Item Class: Stackable Currency\nRarity: Currency\nDeafening Essence of Greed\n--------\nStack Size: 1/9", Game.PoE1).Item!;
        model.BuildMenu(item)[0].Execute(); Assert.Same(essence, model.SelectedLayout);
        model.RememberLoaded(essence); model.Open(); Assert.Same(essence, model.SelectedLayout);
        model.RememberLoaded(general); model.Open(); Assert.Same(general, model.SelectedLayout);
    }

    [Fact] public void SelectionMatchesBaseTypeAndNinjaSlugAndFallsBackForUnknownItems()
    {
        var l = Fixture() with { Cells = [Fixture().Cells[0] with { ItemName = null, NinjaId = "vaal-regalia" }, Fixture().Cells[1]] };
        var general = Fixture() with { TabId = "currency", Page = "general" };
        var model = new StashModel { Layouts = [general, l] };
        var item = ItemTextParser.Parse("Item Class: Body Armours\nRarity: Rare\nDoom Shell\nVaal Regalia\n--------\nItem Level: 85", Game.PoE1).Item!;
        model.BuildMenu(item)[0].Execute(); Assert.Same(l, model.SelectedLayout);
        model.RememberLoaded(l);
        model.BuildMenu(new ParsedItem())[0].Execute(); Assert.Same(l, model.SelectedLayout);
        var empty = new StashModel(); empty.Open(); Assert.Null(empty.SelectedLayout);
    }
    [Fact] public void AmbiguousMatchesPreferLastLoadedThenStableTabAndPageOrder()
    {
        var atlas = Fixture() with { TabId = "currency", Page = "atlas" };
        var general = Fixture() with { TabId = "currency", Page = "general" };
        var other = Fixture() with { TabId = "other", Page = "a" };
        var unmatched = Fixture() with { TabId = "unmatched", Cells =
            [Fixture().Cells[0] with { ItemName = "Unknown" }, Fixture().Cells[1]] };
        var item = ItemTextParser.Parse("Item Class: Stackable Currency\nRarity: Currency\nChaos Orb\n--------\nStack Size: 1/20", Game.PoE1).Item!;
        foreach (var layouts in new[] { new[] { other, general, atlas, unmatched }, new[] { unmatched, atlas, general, other } })
        {
            var model = new StashModel { Layouts = layouts };
            model.BuildMenu(item)[0].Execute(); Assert.Same(atlas, model.SelectedLayout);
            model.RememberLoaded(general);
            model.BuildMenu(item)[0].Execute(); Assert.Same(general, model.SelectedLayout);
            model.RememberLoaded(other);
            model.BuildMenu(item)[0].Execute(); Assert.Same(other, model.SelectedLayout);
            model.RememberLoaded(unmatched);
            model.BuildMenu(item)[0].Execute(); Assert.Same(atlas, model.SelectedLayout);
        }
    }

}
