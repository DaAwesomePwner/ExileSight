using ExileSight.Core.Pricing;
using ExileSight.Features.Vaalasync;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class ConversionTests
{
    [Fact]
    public void ConversionUsesRatesAndExposesStaleness()
    {
        var overview = new PriceOverview([new("divine", "Divine Orb", "Currency", null, 200, 1, [], null, null)], 200);
        var result = new PriceResult(overview, DateTimeOffset.UtcNow, true, null);
        var text = Conversions.Describe(new("~b/o", 2, "divine"), result);
        Assert.Contains("400 chaos", text);
        Assert.Contains("2 divine", text);
        Assert.Contains("stale", text);
        Assert.Contains("rate unavailable", Conversions.Describe(new("~price", 3, "unknown"), result));
    }
    [Theory]
    [InlineData("~b/o 100 chaos", "plus10", "~b/o 110 chaos")]
    [InlineData("~price 1.25 divine", "minus10", "~price 1.13 divine")]
    [InlineData("~b/o 0.5 mirror", "plus10", "~b/o 0.55 mirror")]
    public void SuggestionsRoundForCurrency(string note, string choice, string expected) =>
        Assert.Equal(expected, ShopNote.Parse(note)!.Suggest(choice)!.ToString());
}
