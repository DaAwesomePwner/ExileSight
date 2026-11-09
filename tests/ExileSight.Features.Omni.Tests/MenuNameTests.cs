using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Features.Omni;
using Xunit;

namespace ExileSight.Features.Omni.Tests;

public sealed class MenuNameTests
{
    [Theory]
    [InlineData("Item Class: Rings\nRarity: Normal\nIron Ring", "Iron Ring")]
    [InlineData("Item Class: Rings\nRarity: Rare\nDoom Circle\nIron Ring", "Doom Circle")]
    public void PresentationCarriesParsedItemName(string copy, string expected)
    {
        var menu = new OmniMenu();
        OmniMenuPresentation? shown = null;
        menu.Presented += value => shown = value;
        menu.Show(ItemTextParser.Parse(copy, Game.PoE1).Item!);
        Assert.NotNull(shown);
        Assert.Equal(expected, shown.ItemName);
    }
}
