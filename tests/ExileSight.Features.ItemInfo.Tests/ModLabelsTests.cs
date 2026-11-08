using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class ModLabelsTests
{
    [Theory]
    [InlineData("base_maximum_life", "+ max life")]
    [InlineData("base_fire_damage_resistance_%", "+ fire res")]
    [InlineData("additional_strength", "+ strength")]
    [InlineData("local_minimum_added_physical_damage", "+ phys dmg")]
    [InlineData("global_minimum_added_cold_damage", "+ cold dmg")]
    [InlineData("local_attack_speed_+%", "% attack speed")]
    [InlineData("critical_strike_chance_+%", "% crit chance")]
    [InlineData("local_energy_shield", "+ max ES")]
    [InlineData("local_base_physical_damage_reduction_rating", "+ armour")]
    [InlineData("local_base_evasion_rating", "+ evasion")]
    [InlineData("base_maximum_mana", "+ max mana")]
    [InlineData("base_movement_velocity_+%", "% move speed")]
    public void CommonStatsUseOwnShortLabels(string id, string expected) =>
        Assert.Equal(expected, ModLabels.For([id], "unused"));

    [Fact]
    public void HybridUsesFirstStatEvenWhenOnlyLaterStatIsKnown()
    {
        Assert.Equal("+ max life", ModLabels.For(["base_maximum_life", "base_maximum_mana"], "unused"));
        Assert.Equal("+ max mana", ModLabels.For(["base_maximum_mana", "base_maximum_life"], "unused"));
        Assert.Equal("Unknown power", ModLabels.For(["future_stat", "base_maximum_life"], "12 Unknown power"));
    }

    [Theory]
    [InlineData("+135(130-144) to maximum Life", "to maximum Li…")]
    [InlineData("{0}% increased Armour", "increased Arm…")]
    [InlineData("+# to Life", "to Life")]
    [InlineData("-1.25% Test power.", "Test power")]
    [InlineData("  Cannot\n\t be Stunned! ", "Cannot be Stu…")]
    [InlineData("Abcdefghijklmn", "Abcdefghijklmn")]
    [InlineData("Abcdefghijklmno", "Abcdefghijklm…")]
    [InlineData("Abcdefghijkl more", "Abcdefghijkl…")]
    [InlineData("", "Unknown")]
    [InlineData("+12.5% (10-20) {0} #", "Unknown")]
    public void FallbackRemovesNumericSyntaxCollapsesWhitespaceAndCountsEllipsis(string template, string expected)
    {
        Assert.Equal(expected, ModLabels.For([], template));
        Assert.Equal(expected, ModLabels.For(["future_stat"], template));
        Assert.Equal(expected, ModLabels.For(["BASE_MAXIMUM_LIFE"], template));
    }

    [Fact]
    public void HybridCombinationRequiresTheExactOrderedPair()
    {
        Assert.Equal("% phys + acc", ModLabels.For(["local_physical_damage_+%", "local_accuracy_rating"], "unused"));
        Assert.Equal("+ accuracy", ModLabels.For(["local_accuracy_rating", "local_physical_damage_+%"], "unused"));
        Assert.Equal("% phys dmg", ModLabels.For(["local_physical_damage_+%", "local_accuracy_rating", "base_maximum_life"], "unused"));
        Assert.Equal("% phys dmg", ModLabels.For(["local_physical_damage_+%", "future_stat"], "unused"));
    }

    [Theory]
    [InlineData("(crafted)")]
    [InlineData("(implicit)")]
    [InlineData("(implicit) (crafted)")]
    public void CopySuffixDoesNotLeakIntoShortFallback(string suffix) =>
        Assert.Equal("Test", ModLabels.For([], $"12 Test {suffix}"));

    [Fact]
    public void EveryTableEntryFitsTheLabelColumnWithoutTrailingPunctuation()
    {
        Assert.NotEmpty(ModLabels.Entries);
        foreach (var (ids, label) in ModLabels.CombinedEntries)
        {
            Assert.InRange(label.Length, 1, 14);
            Assert.Equal(label.Trim(), label);
            Assert.True(char.IsLetterOrDigit(label[^1]));
            Assert.True(char.IsLetterOrDigit(label[0]) || label[0] is '+' or '%');
            Assert.Same(label, ModLabels.For([ids.First, ids.Second], "unused"));
        }
        foreach (var (id, label) in ModLabels.Entries)
        {
            Assert.InRange(label.Length, 1, 14);
            Assert.Equal(label.Trim(), label);
            Assert.True(char.IsLetterOrDigit(label[^1]), id);
            Assert.True(char.IsLetterOrDigit(label[0]) || label[0] is '+' or '%', id);
            Assert.Same(label, ModLabels.For([id], "unused"));
        }
    }
}
