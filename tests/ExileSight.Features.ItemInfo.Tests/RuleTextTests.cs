using Xunit;
namespace ExileSight.Features.ItemInfo.Tests;
public sealed class RuleTextTests
{
    [Theory]
    [InlineData("+# to Armour\n#% increased Armour")]
    [InlineData("+{0} to Armour\n{1}% increased Armour")]
    public void HybridTemplatesMatchCopiedFixtureLines(string template)
    {
        var item = CalculatorTests.Hybrid();
        var rules = RuleText.Create(template, Highlight.Desired, ["Body Armours", "Shields"]);
        Assert.Equal(4, rules.Length);
        var settings = new ItemInfoSettings([new("Test", rules)]);
        Assert.All(item.Mods.Take(2), line => Assert.Equal(Highlight.Desired, settings.Evaluate(item, line)));
        Assert.Equal(Highlight.None, settings.Evaluate(item, item.Mods[2]));
        Assert.Equal(2, rules.Count(r => r.ItemClass == "Shields"));
    }
    [Fact]
    public void ClosingOverlayKeepsPreviewInMemory()
    {
        var source = new ItemInfoViewModel();
        var result = new ItemInfoCalculator().Calculate(CalculatorTests.Hybrid());
        source.Show(result);
        source.Close();
        Assert.Null(source.Current);
        Assert.Null(source.Card);
        Assert.Same(result, source.LastCopied);
    }
    [Fact]
    public void HashAndIndexedPlaceholdersHaveSameIdentity()
    {
        var line = CalculatorTests.Hybrid().Mods[2];
        foreach (var template in new[] { "+#% to Fire Resistance", "+{0}% to Fire Resistance", "{0}% to Fire Resistance" })
            Assert.Equal(ModRuleKey.Normalize(line.Text), ModRuleKey.Normalize(RuleText.Lines(template).Single()));
    }
}
