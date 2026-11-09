using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class CurrencyButtonsTests
{
    [Fact]
    public void CurrencyGridWithAmbiguousButtonsStaysUnknown()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var page = layouts.Single(l => l.TabId == "currency" && l.Page == "general");
        var size = new Resolution(2000, 837);
        var pixels = GridFixture.Render(page, size, new(1, 0, 0), 1, 1);
        for (var y = 95; y < 140; y++)
        for (var x = 24; x < 510; x++)
        {
            var i = (y * size.Width + x) * 4;
            pixels[i] = pixels[i + 1] = pixels[i + 2] = 30;
        }
        Assert.Null(GridIdentifier.Identify(layouts, size, new(0, 0, size.Width, size.Height, pixels),
            _ => null, buttons: GridFixture.Buttons).Layout);
    }

    [Fact]
    public void HoverCannotChooseBetweenCompetingCurrencyGrids()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var atlas = layouts.Single(l => l.TabId == "currency" && l.Page == "atlas");
        var league = layouts.Single(l => l.TabId == "currency" && l.Page == "league");
        var size = new Resolution(2000, 837);
        var pixels = GridFixture.Render(atlas with { Cells = [.. atlas.Cells, .. league.Cells] }, size, new(1, 0, 0), 1, 1);
        GridFixture.BrightenButton(pixels, size, "atlas", -30);
        GridFixture.BrightenButton(pixels, size, "league", 25);
        Assert.Equal("league", GridFixture.Buttons.Identify(size, new(0, 0, size.Width, size.Height, pixels)).Page);
        var result = GridIdentifier.Identify(layouts, size, new(0, 0, size.Width, size.Height, pixels), _ => null, buttons: GridFixture.Buttons);
        Assert.True(result.Layout is null || result.Layout.Page == "atlas");
    }

    [Fact]
    public void AtlasWithNoButtonLeaderRetainsWithoutAdoptingBaseline()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var atlas = layouts.Single(l => l.TabId == "currency" && l.Page == "atlas");
        var size = new Resolution(2000, 837);
        var pixels = GridFixture.Render(atlas, size, new(1, 0, 0), 1, 1);
        GridFixture.BrightenButton(pixels, size, "league", 45);
        var sample = new GridSample(0, 0, size.Width, size.Height, pixels);
        Assert.Null(GridFixture.Buttons.Identify(size, sample).Page);
        var result = GridIdentifier.Identify(layouts, size, sample,
            l => Geometry.Fit(l.ReferenceResolution, size), buttons: GridFixture.Buttons, displayedPage: atlas);
        Assert.Null(result.Layout);
        Assert.Equal(atlas, result.HoverPage);
        var policy = new StashWatchPolicy();
        var baseline = new double[PageSignature.Length];
        var hover = Enumerable.Repeat(.2, PageSignature.Length).ToArray();
        var now = DateTimeOffset.UnixEpoch;
        policy.Accept(baseline, baseline);
        Assert.False(policy.Retain(now, result.HoverPage == atlas, hover, hover, adoptBaseline: false));
        Assert.True(policy.Identified);
        Assert.Equal(WatchWork.None, policy.Tick(now, hover, hover).Work);
        Assert.Equal(WatchWork.Identify, policy.Tick(now.AddMilliseconds(450), hover, hover).Work);
    }

    [Theory]
    [InlineData("general")]
    [InlineData("atlas")]
    [InlineData("league")]
    public void BrightestButtonRequiresSixLuminanceMargin(string selected)
    {
        var buttons = GridFixture.Buttons;
        var size = buttons.ReferenceResolution;
        GridSample Strip(byte difference)
        {
            var pixels = new byte[458 * 28 * 4];
            for (var y = 0; y < 28; y++)
            for (var x = 0; x < 458; x++)
            {
                var b = buttons.Buttons[selected];
                var value = (byte)(30 + (x + 38 >= b.X && x + 38 < b.X + b.Width ? difference : 0));
                var i = (y * 458 + x) * 4;
                pixels[i] = pixels[i + 1] = pixels[i + 2] = value;
            }
            return new(38, 104, 458, 28, pixels);
        }
        Assert.Equal(selected, buttons.Identify(size, Strip(20)).Page);
        Assert.Null(buttons.Identify(size, Strip(5)).Page);
        Assert.Null(buttons.Identify(size, Strip(0)).Page);
        Assert.Null(buttons.Identify(size, new(0, 0, 1, 1, new byte[4])).Page);
    }
}
