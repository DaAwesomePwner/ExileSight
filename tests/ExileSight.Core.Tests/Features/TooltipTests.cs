using ExileSight.Core.Features;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class TooltipTests
{
    private sealed class Comparison(string? slot) : IItemCardComparisonProvider
    {
        public ItemCardComparison? BuildComparison(ParsedItem item) => slot is null ? null :
            new([new(slot, true, [])], slot);
    }

    [Fact]
    public void CardComparisonsKeepRegistrationOrderAndOmitNull()
    {
        var result = ItemCardComparisons.Build(new ParsedItem(),
            [new Comparison("left"), new Comparison(null), new Comparison("right")]);
        Assert.NotNull(result);
        Assert.Equal(["left", "right"], result.Slots.Select(slot => slot.Slot));
        Assert.Equal("left · right", result.Note);
        Assert.Null(ItemCardComparisons.Build(new ParsedItem(), [new Comparison(null)]));
    }

    [Theory]
    [InlineData(30, 0, 1, false)]
    [InlineData(31, 0, 1, true)]
    [InlineData(60, 0, 2, false)]
    [InlineData(45, 45, 2, true)]
    [InlineData(0, -16, 0.5, true)]
    public void DismissalUsesScaledEuclideanDistance(double x, double y, double scale, bool expected) =>
        Assert.Equal(expected, TooltipDismissal.MovedBeyond(x - 1920, y + 200, -1920, 200, scale));
}
