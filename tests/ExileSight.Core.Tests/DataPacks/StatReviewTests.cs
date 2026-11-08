using ExileSight.Core.DataPacks;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class StatReviewTests
{
    private static StatTranslation Entry(string handler) => new("synthetic", ["value"],
        [new("{0} Glow", [new()], ["#"], [[handler]])]);

    [Theory]
    [InlineData("per_minute_to_per_second", 2000, "33.3", 1995, 2001)]
    [InlineData("per_minute_to_per_second_0dp", 2000, "33", 1950, 2010)]
    [InlineData("per_minute_to_per_second_1dp", 120, "2", 117, 123)]
    [InlineData("per_minute_to_per_second_2dp", 2000, "33.33", 2000, 2000)]
    [InlineData("per_minute_to_per_second_2dp_if_required", 120, "2", 120, 120)]
    [InlineData("milliseconds_to_seconds", 1500, "1.5", 1500, 1500)]
    [InlineData("milliseconds_to_seconds_0dp", 2000, "2", 1500, 2500)]
    [InlineData("milliseconds_to_seconds_1dp", 1500, "1.5", 1450, 1550)]
    [InlineData("milliseconds_to_seconds_2dp", 1500, "1.5", 1495, 1505)]
    [InlineData("milliseconds_to_seconds_2dp_if_required", 1500, "1.5", 1495, 1505)]
    [InlineData("divide_by_one_hundred", 150, "1.5", 150, 150)]
    [InlineData("divide_by_one_hundred_2dp", 150, "1.5", 150, 150)]
    [InlineData("divide_by_one_hundred_2dp_if_required", 150, "1.5", 150, 150)]
    [InlineData("divide_by_ten_1dp", 20, "2", 20, 20)]
    [InlineData("divide_by_ten_1dp_if_required", 20, "2", 20, 20)]
    [InlineData("divide_by_two_0dp", 5, "2", 4, 5)]
    [InlineData("divide_by_ten_0dp", -15, "-2", -20, -11)]
    [InlineData("divide_by_fifteen_0dp", 31, "2", 30, 44)]
    [InlineData("old_leech_percent", 15, "3", 15, 15)]
    [InlineData("old_leech_permyriad", 750, "1.5", 750, 750)]
    [InlineData("negate", 15, "-15", 15, 15)]
    [InlineData("double", 15, "30", 15, 15)]
    [InlineData("negate_and_double", 15, "-30", 15, 15)]
    [InlineData("deciseconds_to_seconds", 20, "2", 20, 20)]
    [InlineData("locations_to_metres", 20, "2", 20, 20)]
    [InlineData("divide_by_one_thousand", 1500, "1.5", 1500, 1500)]
    [InlineData("divide_by_three", 15, "5", 15, 15)]
    [InlineData("divide_by_six", 12, "2", 12, 12)]
    [InlineData("divide_by_twelve", 24, "2", 24, 24)]
    [InlineData("divide_by_four", 6, "1.5", 6, 6)]
    [InlineData("divide_by_five", 15, "3", 15, 15)]
    [InlineData("divide_by_twenty", 30, "1.5", 30, 30)]
    [InlineData("times_twenty", 2, "40", 2, 2)]
    [InlineData("times_one_point_five", 3, "4.5", 3, 3)]
    [InlineData("30%_of_value", 5, "1.5", 5, 5)]
    [InlineData("60%_of_value", 5, "3", 5, 5)]
    [InlineData("divide_by_one_hundred_and_negate", 150, "-1.5", 150, 150)]
    [InlineData("canonical_line", 15, "15", 15, 15)]
    [InlineData("canonical_stat", 15, "15", 15, 15)]
    public void RendersNormalizedTextAndReturnsAllPossibleIntegers(string handler, decimal raw, string text, decimal min, decimal max)
    {
        var entry = Entry(handler);
        Assert.Equal(text + " Glow", StatText.Render(entry, [raw]));
        Assert.Equal(new StatValue("value", min, max), Assert.Single(StatText.Match(text + " Glow", [entry])).Values[0]);
    }

    [Fact]
    public void MultilineCopyReturnsOnlyStatsVisibleOnThatLine()
    {
        var entry = new StatTranslation("hybrid", ["a", "b"],
            [new("{0} Glow\n{1} Shine", [new(), new()], ["#", "#"], [[], []])]);
        Assert.Equal("4 Glow\n7 Shine", StatText.Render(entry, [4, 7]));
        Assert.Equal(new StatValue("a", 4, 4), Assert.Single(StatText.Match("4 Glow", [entry])).Values.Single());
        Assert.Equal(new StatValue("b", 7, 7), Assert.Single(StatText.Match("7 Shine", [entry])).Values.Single());
    }

    [Theory]
    [InlineData("(crafted)")]
    [InlineData("(implicit)")]
    [InlineData("(fractured)")]
    [InlineData("(enchant)")]
    [InlineData("— Unscalable Value")]
    [InlineData("(crafted) — Unscalable Value")]
    public void CallerCanStripOnlyKnownTrailingAnnotations(string suffix)
    {
        var entry = Entry("canonical_line");
        Assert.Empty(StatText.Match("4 Glow " + suffix, [entry]));
        Assert.Single(StatText.Match(StatText.StripCopySuffixes("4 Glow " + suffix), [entry]));
        Assert.Equal("text (unknown)", StatText.StripCopySuffixes("text (unknown)"));
    }

    [Fact]
    public void PathologicalInputFailsClosedAndPreparedPatternsCanBeReused()
    {
        var entry = Entry("canonical_line");
        StatText.Prepare([entry]);
        Assert.Single(StatText.Match("4 Glow", [entry]));
        Assert.Single(StatText.Match("5 Glow", [entry]));
        var ids = Enumerable.Range(0, 40).Select(i => "s" + i).ToArray();
        var adversarial = new StatTranslation("slow", ids, [new(
            string.Concat(Enumerable.Range(0, 40).Select(i => "{" + i + "}")) + "!",
            ids.Select(_ => new StatCondition()).ToArray(), ids.Select(_ => "#").ToArray(),
            ids.Select(_ => Array.Empty<string>()).ToArray())]);
        Assert.Empty(StatText.Match(new string('1', 4000) + "?", [adversarial]));
    }

    [Fact]
    public void RoundedCandidatesAreClippedToConditions()
    {
        var entry = Entry("per_minute_to_per_second");
        entry = entry with { English = [entry.English[0] with { Conditions = [new(1999, 2000)] }] };
        Assert.Equal(new StatValue("value", 1999, 2000), Assert.Single(StatText.Match("33.3 Glow", [entry])).Values[0]);
    }
}
