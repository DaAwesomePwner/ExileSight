using ExileSight.Core.Features;
using ExileSight.Core.Logs;
using Xunit;

namespace ExileSight.Features.GearCmp.Tests;

public sealed class StoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "gearcmp-test-" + Guid.NewGuid().ToString("N"));
    private GearModel Open() => new(new GearStore(directory));
    private static EquippedGear Gear(string type, decimal life) => new(type, new() { ["Life"] = life });
    [Fact]
    public void CandidatesAreEphemeralAndPartyEventsNeverSwitchProfiles()
    {
        var model = Open();
        var feature = new GearCmpFeature(model);
        var host = new FeatureHost([feature], (_, _) => null, (_, _, _) => { });
        host.Publish(new CharacterLevelUp("", null, "Owner", "Witch", 10));
        host.Publish(new CharacterLevelUp("", null, "PartyMember", "Witch", 10));
        Assert.False(Directory.Exists(directory));
        model.Confirm("Owner");
        model.Equip("Owner", GearSlot.Helmet, Gear("Helmets", 50));
        host.Publish(new CharacterLevelUp("", null, "PartyMember", "Witch", 11));
        Assert.Equal("Owner", model.Character);
        Assert.DoesNotContain("PartyMember", string.Join("", Directory.GetFiles(directory).Select(File.ReadAllText)));
        var restarted = Open();
        Assert.Equal("Owner", restarted.Character);
        Assert.Empty(restarted.Candidates);
        Assert.Equal(50, Assert.Single(restarted.Replaced(GearSlot.Helmet, Gear("Helmets", 0))).Stats["Life"]);
    }
    [Fact]
    public void ProfilesRemainSeparateAndDefaultSurvivesRestart()
    {
        var model = Open();
        model.Equip(null, GearSlot.LeftRing, Gear("Rings", 10));
        model.Observe("A"); model.Confirm("A");
        Assert.False(model.Has(GearSlot.LeftRing));
        model.Equip("A", GearSlot.LeftRing, Gear("Rings", 20));
        model.Observe("B"); model.Confirm("B");
        Assert.False(model.Has(GearSlot.LeftRing));
        model.Confirm(null);
        Assert.Equal(10, Assert.Single(Open().Replaced(GearSlot.LeftRing, Gear("Rings", 0))).Stats["Life"]);
    }
    [Fact]
    public void TwoHanderReplacesBothHandsAndOneHandReplacesTwoHander()
    {
        var model = Open();
        model.Equip(null, GearSlot.MainHand, Gear("One Hand Swords", 10));
        model.Equip(null, GearSlot.OffHand, Gear("Shields", 20));
        var two = Gear("Two Hand Swords", 50);
        Assert.Equal(20, GearStats.Delta(two, model.Replaced(GearSlot.MainHand, two))["Life"]);
        model.Equip(null, GearSlot.MainHand, two);
        Assert.False(model.Has(GearSlot.OffHand));
        Assert.Single(model.Replaced(GearSlot.OffHand, Gear("Shields", 0)));
        model.Equip(null, GearSlot.OffHand, Gear("Shields", 20));
        Assert.False(model.Has(GearSlot.MainHand));
        Assert.True(Open().Has(GearSlot.OffHand));
    }
    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("""{"SchemaVersion":1,"Slots":null}""")]
    public void CorruptionIsQuarantined(string json)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "default.json"), json);
        var model = Open();
        Assert.NotNull(model.Notice);
        Assert.Single(Directory.GetFiles(directory, "*.corrupt"));
        model.Equip(null, GearSlot.Helmet, Gear("Helmets", 10));
        Assert.True(Open().Has(GearSlot.Helmet));
    }
    [Fact]
    public void CorruptActiveProfileFallsBackAndClearSurvivesRestart()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "active.json"), "broken");
        var model = Open();
        Assert.Null(model.Character);
        Assert.NotNull(model.Notice);
        model.Equip(null, GearSlot.Helmet, Gear("Helmets", 10));
        model.Clear(null, GearSlot.Helmet);
        Assert.False(Open().Has(GearSlot.Helmet));
    }
    [Fact]
    public void TooltipUsesSignedTonesForSameSlot()
    {
        var model = Open();
        model.Equip(null, GearSlot.Helmet, new("Helmets", new() { ["Armour"] = 100, ["Ward"] = 5 }));
        var item = StatsTests.Item("Helmets");
        item.Properties["Armour"] = new("120", []);
        var section = new GearCmpFeature(model).Build(item)!;
        var texts = section.Rows.SelectMany(r => r.Text).ToArray();
        Assert.Contains(texts, t => t.Text == "Armour: +20" && t.Tone == TooltipTone.High);
        Assert.Contains(texts, t => t.Text == "Ward: -5" && t.Tone == TooltipTone.Low);
        Assert.DoesNotContain(new GearCmpFeature(model).Build(StatsTests.Item("Boots"))!.Rows.SelectMany(r => r.Text),
            t => t.Text.StartsWith("Armour:", StringComparison.Ordinal));
    }
    [Fact]
    public void CandidateBoundAndSafeFilenames()
    {
        var model = Open();
        for (var i = 0; i < 10; i++) model.Observe("Name" + i);
        Assert.Equal(5, model.Candidates.Length);
        Assert.Equal("Name9", model.Candidates[0]);
        Assert.DoesNotContain("/", GearStore.ProfileFile("../CON"));
        Assert.NotEqual(GearStore.ProfileFile("a/b"), GearStore.ProfileFile("a_b"));
    }
    [Fact]
    public void MenuActionsAndTooltipAreLocalAndProfileBound()
    {
        var model = Open();
        var feature = new GearCmpFeature(model);
        var item = StatsTests.Item("Rings");
        var entries = feature.BuildMenu(item);
        entries.Single(e => e.Label.StartsWith("Set as left ring", StringComparison.Ordinal)).Execute();
        Assert.True(model.Has(GearSlot.LeftRing));
        Assert.Contains(feature.Build(item)!.Rows, r => r.Text.Any(t => t.Text.Contains("Default profile", StringComparison.Ordinal)));
        model.Observe("A"); model.Confirm("A");
        Assert.Throws<InvalidOperationException>(() => entries.Single(e => e.Label.StartsWith("Set as right ring", StringComparison.Ordinal)).Execute());
        Assert.Empty(feature.Manifest.GameActions);
        Assert.Empty(feature.Manifest.NetworkHosts);
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
