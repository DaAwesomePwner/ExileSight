using ExileSight.Core.Features;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class TooltipTests
{
    private sealed class Section(int priority, string? id) : ITooltipSectionProvider
    {
        public int Priority => priority;
        public TooltipSection? Build(ParsedItem item) => id is null ? null : new(id, id, []);
    }
    [Fact]
    public void SectionsAreStableAndInapplicableSectionsAreOmitted()
    {
        var result = TooltipSections.Build(new ParsedItem(),
            [new Section(200, "gear"), new Section(100, "item"), new Section(100, "second"), new Section(0, null)]);
        Assert.Equal(["item", "second", "gear"], result.Select(s => s.Id));
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
