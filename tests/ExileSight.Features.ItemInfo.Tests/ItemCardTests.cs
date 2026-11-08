using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class ItemCardTests
{
    [Fact]
    public void HybridRowsSpanGameLinesAndPreserveCalculatorTones()
    {
        var result = new ItemInfoCalculator().Calculate(CalculatorTests.Hybrid());
        var card = ItemCardBuilder.Build(result);
        Assert.Equal(2, card.Rows.Count);
        Assert.Equal(2, card.Rows[0].GameLineCount);
        Assert.Equal(0, card.Rows[0].GameLineStart);
        Assert.Equal(2, card.Rows[1].GameLineStart);
        Assert.Equal(50, card.Rows[0].RollPosition);
        Assert.Equal(InfoColor.High, card.Rows[0].TierTone);
        Assert.Equal(3, card.Slots!.Prefixes.Maximum);
    }
}
