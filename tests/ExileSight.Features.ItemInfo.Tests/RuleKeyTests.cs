using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class RuleKeyTests
{
    [Fact]
    public void InversionAndHighlightsShareExactNormalizedKey()
    {
        var item = CalculatorTests.Hybrid();
        var line = item.Mods[0] with { Text = "+12(5-15)   to ARMOUR (fractured)", Values = [new(12, 5, 15)] };
        item.Mods.Clear();
        item.Mods.Add(line);
        var settings = new ItemInfoSettings([new("Test", [new("+10(5-15) to Armour", Highlight.Desired)])],
            InvertedLines: ["+10(5-15) to Armour (crafted)"]);
        var row = Assert.Single(new ItemInfoCalculator().Calculate(item, settings).Mods);
        Assert.Equal(30, row.RollPosition);
        Assert.Equal(Highlight.Desired, row.Highlight);
        Assert.Equal(Highlight.None, settings.Evaluate(item, line with { Text = "+12 to Armour while stationary" }));
        Assert.Equal(ModRuleKey.Normalize("+10(5-15)% increased Test"),
            ModRuleKey.Normalize("+12(5-15)%  INCREASED Test (implicit)"));
    }

    [Fact]
    public void EnchantsUseTheirOwnRules()
    {
        var item = CalculatorTests.Hybrid();
        var line = item.Mods[0] with { Kind = ModKind.Enchant };
        var settings = new ItemInfoSettings([new("Test", [
            new(line.Text, Highlight.Undesired), new(line.Text, Highlight.Undesired, Implicit: true),
            new(line.Text, Highlight.Desired, Enchant: true)])]);
        Assert.Equal(Highlight.Desired, settings.Evaluate(item, line));
        Assert.Equal(Highlight.Undesired, settings.Evaluate(item, line with { Kind = ModKind.Implicit }));
    }

    [Fact]
    public void UniqueImplicitKeepsImplicitTier()
    {
        var item = Core.Items.ItemTextParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures/eldritch.txt")), Core.Games.Game.PoE1).Item!;
        var rows = new ItemInfoCalculator().Calculate(item).Mods;
        Assert.Equal(new int?[] { 6, 5, 4, 3, 2, 1 }, rows.Select(m => m.Source.Tier));
        Assert.All(rows, row => Assert.StartsWith("Implicit", row.TierLabel));
    }
}
