using System.Text.Json;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Shell;
using Xunit;

namespace ExileSight.Features.Omni.Tests;

public sealed class LinkEscapingTests
{
    [Theory]
    [InlineData("O'Brien")]
    [InlineData("Ω 東京")]
    [InlineData("A#B?C")]
    [InlineData("A\"B\\C")]
    public void SpecialCharactersRemainDataInPagesAndTradeJson(string text)
    {
        var item = ItemTextParser.Parse($"Item Class: Rings\nRarity: Unique\n{text}\n{text}", Game.PoE1).Item!;
        var links = OmniLinks.Build(item, text);
        foreach (var link in links) BrowserUrl.Validate(link.Url, OmniLinks.AllowedHosts);
        var wiki = new Uri(links[0].Url);
        Assert.Empty(wiki.Query);
        Assert.Empty(wiki.Fragment);
        Assert.Equal("/wiki/" + text.Replace(' ', '_'), Uri.UnescapeDataString(wiki.AbsolutePath));
        var trade = new Uri(links[3].Url);
        Assert.Equal("www.pathofexile.com", trade.Host);
        Assert.Empty(trade.Fragment);
        Assert.Equal("/trade/search/" + text, Uri.UnescapeDataString(trade.AbsolutePath));
        using var json = JsonDocument.Parse(Uri.UnescapeDataString(trade.Query["?q=".Length..]));
        Assert.Equal(text, json.RootElement.GetProperty("query").GetProperty("name").GetString());
        Assert.Equal(text, json.RootElement.GetProperty("query").GetProperty("type").GetString());
    }
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void DotLeagueCannotEscapeSearchPath(string league)
    {
        var item = ItemTextParser.Parse("Item Class: Rings\nRarity: Normal\nGold Ring", Game.PoE1).Item!;
        Assert.Throws<ArgumentException>(() => OmniLinks.Build(item, league));
    }
    [Theory]
    [InlineData("Configured", "Challenge", "Configured")]
    [InlineData(null, "Challenge", "Challenge")]
    [InlineData(null, null, "Standard")]
    public void TypedPricesSettingWinsOtherwiseUseDiscoveryDefaultOrStandard(
        string? configured, string? discovered, string expected)
    {
        var settings = new SettingsDocument();
        settings.Prices.League = configured;
        LeagueResult? leagues = discovered is null ? null : new(["Standard", discovered], discovered, false, null);
        Assert.Equal(expected, OmniLeague.Select(settings.Prices.League, leagues));
    }
}
