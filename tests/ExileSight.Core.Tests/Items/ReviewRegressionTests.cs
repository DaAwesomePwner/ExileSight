using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Core.Tests.Items;

public sealed class ReviewRegressionTests
{
    private static ItemParseResult Copy(string body, string name = "Gold Ring", string rarity = "Normal", string itemClass = "Rings") =>
        ItemTextParser.Parse($"Item Class: {itemClass}\nRarity: {rarity}\n{name}\n--------\n{body}", Game.PoE1);

    [Theory]
    [InlineData("Superior Glorious Plate", "Glorious Plate", false)]
    [InlineData("Exceptional Fireball", "Fireball", false)]
    [InlineData("Synthesised Gold Ring", "Gold Ring", true)]
    public void NormalizesPresentationPrefixes(string name, string expected, bool synthesised)
    {
        var item = Copy("Quality: +20%\n--------\nItem Level: 84", name).Item!;
        Assert.Equal(expected, item.Name);
        Assert.Equal(expected, item.BaseType);
        Assert.Equal(synthesised, item.Synthesised);
    }

    [Theory]
    [InlineData("Recovers 2400 Life over 7 Seconds\nConsumes 15 of 45 Charges on use\nCurrently has 45 Charges", "Life Flasks")]
    [InlineData("Chance to Block: 24%", "Shields")]
    [InlineData("Spell, Projectile, Fire\nLevel: 1\nCost: 6 Mana\nReservation: 25% Mana\nCast Time: 0.75 sec\nCritical Strike Chance: 6%\nDamage Effectiveness: 370%\nCooldown Time: 1 sec\nSouls Per Use: 20", "Skill Gems")]
    [InlineData("More Scarabs: +50%\nMore Currency: +60%\nMore Maps: +70%\nArea Level: 83\nMonster Pack Size: +20%", "Maps")]
    [InlineData("Limited to: 1\nRadius: Large", "Jewels")]
    public void PropertySectionsNeverBecomeMods(string text, string itemClass)
    {
        var result = Copy(text + "\n--------\nItem Level: 84", rarity: itemClass.EndsWith("Gems", StringComparison.Ordinal) ? "Gem" : "Normal", itemClass: itemClass);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Item!.Mods);
        Assert.Empty(result.Item.OtherText);
        Assert.Equal(text.Split('\n').Length + 1, result.Item.Properties.Count);
    }

    [Fact]
    public void MagicNamesUseBothAdvancedAffixes()
    {
        const string name = "Healthy Gold Ring of the Fox";
        var plain = Copy("Item Level: 84\n--------\n+20 to maximum Life\n+10 to Dexterity", name, "Magic");
        var advanced = Copy("Item Level: 84\n--------\n{ Prefix Modifier \"Healthy\" (Tier: 3) }\n+20(20-29) to maximum Life\n{ Suffix Modifier \"of the Fox\" (Tier: 4) }\n+10(10-15) to Dexterity", name, "Magic");
        Assert.Null(plain.Item!.BaseType);
        Assert.Equal("Gold Ring", advanced.Item!.BaseType);
    }

    [Fact]
    public void GreaterDoesNotMeanEater()
    {
        var result = Copy("{ Searing Exarch Implicit Modifier (Greater) }\n+12% to Fire Resistance (implicit)\n--------\n{ Prefix Modifier \"Greater\" (Tier: 1) }\n+20 to maximum Life\n--------\nSearing Exarch Item");
        Assert.DoesNotContain(result.Item!.Mods, m => m.Kind.HasFlag(ModKind.Eater));
        Assert.Equal(["Searing Exarch"], result.Item.Influences);
    }

    [Fact]
    public void DigitFreeModsAndInstructionsAgreeAcrossCopyModes()
    {
        const string tail = "Iron Reflexes\nHas no Sockets\n--------\nRight click to use this item.";
        var plain = Copy("Item Level: 84\n--------\n" + tail).Item!;
        var advanced = Copy("Item Level: 84\n--------\n{ Unique Modifier }\n" + tail).Item!;
        Assert.Equal(new[] { "Iron Reflexes", "Has no Sockets" }, plain.Mods.Select(m => m.Text));
        Assert.Equal(plain.Mods.Select(m => m.Text), advanced.Mods.Select(m => m.Text));
        Assert.Equal(plain.OtherText, advanced.OtherText);
        Assert.Single(plain.OtherText);
    }

    [Theory]
    [InlineData("Lesser")]
    [InlineData("Greater")]
    [InlineData("Grand")]
    [InlineData("Exceptional")]
    [InlineData("Exquisite")]
    [InlineData("Perfect")]
    public void NamedEldritchTiersKeepCopyModesConsistent(string tier)
    {
        const string trailers = "\n--------\nSearing Exarch Item\nEater of Worlds Item";
        var plain = Copy("+12% to Fire Resistance (implicit)\n+12% to Cold Resistance (implicit)" + trailers).Item!;
        var advanced = Copy($"{{ Searing Exarch Implicit Modifier ({tier}) }}\n+12% to Fire Resistance (implicit)\n{{ Eater of Worlds Implicit Modifier ({tier}) }}\n+12% to Cold Resistance (implicit)" + trailers).Item!;
        Assert.Equal(plain.Influences, advanced.Influences);
        Assert.Equal(new[] { "Searing Exarch", "Eater of Worlds" }, plain.Influences);
        Assert.True(advanced.Mods[0].Kind.HasFlag(ModKind.Exarch));
        Assert.False(advanced.Mods[0].Kind.HasFlag(ModKind.Eater));
        Assert.True(advanced.Mods[1].Kind.HasFlag(ModKind.Eater));
        Assert.Contains(tier, advanced.Mods[0].Header!);
    }

    [Fact]
    public void ColonWithinModSectionIsNotAProperty()
    {
        var item = Copy("Item Level: 84\n--------\nAdds 8 Passive Skills (enchant)\nAdded Small Passive Skills grant: 12% increased Damage (enchant)").Item!;
        Assert.Equal(2, item.EnchantMods.Count());
        Assert.Single(item.Properties);
    }
}
