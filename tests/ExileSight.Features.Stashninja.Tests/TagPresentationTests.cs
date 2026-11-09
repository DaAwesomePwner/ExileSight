using ExileSight.Features.Stashninja;
using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class TagPresentationTests
{
    [Theory]
    [InlineData("#000000", "#FFFFFF")]
    [InlineData("#FFFFFF", "#000000")]
    [InlineData("#6FE3D5", "#000000")]
    [InlineData("#6AA8FF", "#000000")]
    [InlineData("#757575", "#FFFFFF")]
    [InlineData("#767676", "#000000")]
    [InlineData("#FF0000", "#000000")]
    [InlineData("#0000FF", "#FFFFFF")]
    public void UsesHigherWcagContrast(string background, string foreground) =>
        Assert.Equal(foreground, TagPresentation.Foreground(background));

    [Fact] public void FormatsUnitPricesAtDivineBoundary()
    {
        Assert.Equal("45", TagPresentation.Price(45, .3m));
        Assert.Equal("199.99", TagPresentation.Price(199.99m, .99995m));
        Assert.Equal("1d", TagPresentation.Price(200, 1));
        Assert.Equal("1.4d", TagPresentation.Price(280, 1.4m));
        Assert.Equal("280", TagPresentation.Price(280, null));
        Assert.Equal("0.25", TagPresentation.Price(.25m, null));
    }

    [Theory]
    [InlineData("3.1d", 4, "3.1d")]
    [InlineData("3.1d", 3, "3d")]
    [InlineData("3.19d", 4, "3.1d")]
    [InlineData("1234", 4, "1234")]
    [InlineData("12345", 5, "12345")]
    [InlineData("12345", 4, "12k")]
    [InlineData("1234", 3, "1k")]
    [InlineData("1234", 2, "1k")]
    [InlineData("999", 2, null)]
    [InlineData("0.25", 3, "0.2")]
    [InlineData("0.25", 1, null)]
    [InlineData("3.1d", 1, null)]
    public void ChoosesCompleteConservativePriceOrOmits(string price, int capacity, string? expected) =>
        Assert.Equal(expected, TagPresentation.FitPrice(price, text => text.Length <= capacity));

    [Fact] public void UsesMeasuredFitRatherThanCharacterCount()
    {
        Assert.Equal("1.2k", TagPresentation.FitPrice("1234", text => text == "1.2k"));
        Assert.Null(TagPresentation.FitPrice("3.1d", _ => false));
        Assert.Equal("1.2m", TagPresentation.FitPrice("1234567", text => text.Length <= 4));
    }

    private static PriceTag Tag(string name, decimal chaos) => new(
        new(name, 10, 20, 40, 40, "Currency", ItemName: name), name, chaos, "1", "#FFFFFF", []);

    [Fact] public void SelectsTopThreeWithoutSummingStacks()
    {
        var tags = new[] { Tag("C", 10), Tag("B", 20), Tag("D", 1), Tag("A", 20) };
        Assert.Equal(new[] { "A", "B", "C" }, TagPresentation.Highest(tags).Select(t => t.Name));
        Assert.Single(TagPresentation.Highest(tags.Take(1)));
        Assert.Empty(TagPresentation.Highest([]));
    }

    [Fact] public void AgeIsConservativeAndMarksStalePrices()
    {
        var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        Assert.Equal("14 min ago", TagPresentation.Age(now.AddMinutes(-14), now));
        Assert.Equal("2 h ago · stale", TagPresentation.Age(now.AddHours(-2), now));
        Assert.Equal("price age unknown", TagPresentation.Age(null, now));
        Assert.Equal("just now", TagPresentation.Age(now.AddMinutes(1), now));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.72)]
    public void CornerBoundsLeaveStackCountCornerFree(double scale)
    {
        var cell = Tag("A", 1).Cell;
        var layout = new Layout("currency", "general", new(1920, 1080), [cell], [], new("test", "test", new(2026, 1, 1)));
        var transform = new Transform(scale, 5, 7);
        var bounds = Geometry.CornerTagBounds(layout, cell, transform, 24);
        Assert.Equal((cell.X + cell.W - 2) * scale + 5, bounds.X + bounds.Width, 8);
        Assert.Equal((cell.Y + cell.H - 2) * scale + 7, bounds.Y + bounds.Height, 8);
        Assert.Equal(14 * scale, bounds.Height, 8);
        Assert.True(bounds.Y > cell.Y * scale + 7);
    }
}
