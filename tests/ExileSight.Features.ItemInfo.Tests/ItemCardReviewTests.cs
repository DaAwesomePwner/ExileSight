using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

// card-white copies are intentionally identical: no mod headers distinguish their copy mode.
// All card-* fixtures are hand-written synthetic examples (D-044).
public sealed class ItemCardReviewTests
{
    [Theory]
    [InlineData("plain")]
    [InlineData("advanced")]
    public void NoModCopyDoesNotNagOrShowSlots(string mode)
    {
        var item = ItemCardStateTests.Read($"iteminfo/card-white.{mode}.txt");
        var card = ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(item));
        Assert.Empty(card.Rows);
        Assert.False(card.NeedsAdvancedCopy);
        Assert.Null(card.Slots);
    }

    [Fact]
    public void VerdictRequiresProfileAndNonzeroCounts()
    {
        var item = ItemCardStateTests.Read("iteminfo/card-ring.txt");
        Assert.Null(ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(item)).Verdict);
        var settings = new ItemInfoSettings([new("Empty", [])]);
        Assert.Null(ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(item, settings)).Verdict);
    }

    [Fact]
    public void BuilderUsesCalculatorSettingsAndAvoidWinsWithinHybrid()
    {
        var item = ItemCardStateTests.Read("iteminfo/card-weapon.txt");
        var settings = new ItemInfoSettings([new("Conflicting", [
            new(item.Mods[0].Text, Highlight.Desired), new(item.Mods[1].Text, Highlight.Undesired)])],
            ShowItemLevelColumn: false);
        var card = ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(item, settings));
        Assert.Equal(Highlight.Undesired, card.Rows[0].Highlight);
        Assert.Equal(0, card.Verdict!.Wanted);
        Assert.Equal(1, card.Verdict.Unwanted);
        Assert.False(card.ShowItemLevelColumn);
        Assert.Equal(item.Name, card.Name.FullName);
    }

    [Fact]
    public void FixedUniqueImplicitAndEnchantKeepKindPills()
    {
        var item = ItemCardStateTests.Read("iteminfo/card-fixed-unique.txt");
        var card = ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(item));
        Assert.Equal(["ench", "imp", "·"], card.Rows.Select(r => r.Tier));
        Assert.All(card.Rows, r => Assert.True(r.Fixed));
        Assert.Equal(CardSlotKind.None, card.Rows[0].Slot);
        Assert.Equal(CardSlotKind.Implicit, card.Rows[1].Slot);
        Assert.Equal(CardSlotKind.None, card.Rows[2].Slot);
    }

    [Fact]
    public void PlainExplicitAffixKeepsUnknownSlot()
    {
        var item = ItemCardStateTests.Read("rings/two-affix-magic.plain.txt");
        var card = ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(item));
        Assert.All(card.Rows.Where(r => r.Slot != CardSlotKind.Implicit),
            r => Assert.Equal(CardSlotKind.Unknown, r.Slot));
    }
}
