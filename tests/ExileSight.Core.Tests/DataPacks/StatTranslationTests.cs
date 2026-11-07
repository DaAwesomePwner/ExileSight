using ExileSight.Core.DataPacks;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class StatTranslationTests
{
    private static StatTranslation Entry(string id = "life", string text = "{0} to maximum Life",
        string handler = "", StatCondition? condition = null) =>
        new(id, [id], [new(text, [condition ?? new()], ["+#"], [handler == "" ? [] : [handler]])]);

    [Fact]
    public void MatchesSignedValuesRangesAndAllAmbiguousCandidates()
    {
        var entries = new[] { Entry(), Entry("other") };
        Assert.Equal(2, StatText.Match("+45 to maximum Life", entries).Length);
        Assert.Equal(new StatValue("life", 45, 45), StatText.Match("+45 to maximum Life", entries)[0].Values[0]);
        Assert.Equal(new StatValue("life", 10, 20), StatText.Match("+(10–20) to maximum Life", entries)[0].Values[0]);
        Assert.Equal(45, StatText.Match("+45(10-50) to maximum Life", entries)[0].Values[0].Min);
        Assert.Equal("+45 to maximum Life", StatText.Render(entries[0], [45]));
    }

    [Fact]
    public void ConditionsAndNegationApplyToRawValues()
    {
        var entry = Entry(text: "{0}% reduced Armour", handler: "negate", condition: new(Max: -1));
        Assert.Equal(-20, Assert.Single(StatText.Match("+20% reduced Armour", [entry])).Values[0].Min);
        Assert.Equal("+20% reduced Armour", StatText.Render(entry, [-20]));
        Assert.Null(StatText.Render(entry, [20]));
        Assert.Empty(StatText.Match("-20% reduced Armour", [entry]));
        Assert.Null(StatText.Render(Entry(condition: new(1, 10, true)), [5]));
    }

    [Theory]
    [InlineData("per_minute_to_per_second", 120, 2)]
    [InlineData("milliseconds_to_seconds", 1500, 1.5)]
    [InlineData("divide_by_one_hundred", 150, 1.5)]
    public void ReversesHandlers(string handler, decimal raw, decimal display)
    {
        var entry = Entry(handler: handler);
        var rendered = StatText.Render(entry, [raw])!;
        Assert.Contains(display.ToString(System.Globalization.CultureInfo.InvariantCulture), rendered);
        Assert.Equal(raw, Assert.Single(StatText.Match(rendered, [entry])).Values[0].Min);
    }

    [Fact]
    public void HybridAndRepeatedPlaceholdersPreserveStatIdentity()
    {
        var entry = new StatTranslation("hybrid", ["a", "b"],
            [new("Adds {0} to {1} Damage ({0})", [new(), new()], ["#", "#"], [[], []])]);
        Assert.Equal(new[] { new StatValue("a", 3, 3), new StatValue("b", 7, 7) },
            Assert.Single(StatText.Match("Adds 3 to 7 Damage (3)", [entry])).Values);
        Assert.Empty(StatText.Match("Adds 3 to 7 Damage (4)", [entry]));
    }

    [Fact]
    public void MatchesRangeOnlyAndChoosesFirstApplicableRendering()
    {
        var entry = new StatTranslation("armour", ["armour"],
            [new("{0}% increased Armour", [new(Min: 1)], ["#"], [[]]),
             new("{0}% reduced Armour", [new(Max: -1)], ["#"], [["negate"]])]);
        Assert.Equal(new StatValue("armour", 10, 20),
            Assert.Single(StatText.Match("(10–20)% increased Armour", [entry])).Values[0]);
        Assert.Equal("20% reduced Armour", StatText.Render(entry, [-20]));
        Assert.Equal(-20, Assert.Single(StatText.Match("20% reduced Armour", [entry])).Values[0].Min);
        Assert.Empty(StatText.Match("noise 20% reduced Armour", [entry]));
    }

    [Fact]
    public void IgnoredStatsRequireAnExactRecoverableCondition()
    {
        var entry = new StatTranslation("flag", ["flag"], [new("Synthetic flag", [new(1, 1)], ["ignored"], [[]])]);
        Assert.Equal(1, Assert.Single(StatText.Match("Synthetic flag", [entry])).Values[0].Min);
        Assert.Empty(StatText.Match("Synthetic flag", [entry with {
            English = [new("Synthetic flag", [new(Min: 1)], ["ignored"], [[]])] }]));
    }

    [Fact]
    public void HiddenAndUnsupportedHandlersDoNotInventValues()
    {
        Assert.Null(StatText.Render(Entry() with { Hidden = true }, [1]));
        Assert.Empty(StatText.Match("+1 to maximum Life", [Entry(handler: "unknown")]));
    }
}
