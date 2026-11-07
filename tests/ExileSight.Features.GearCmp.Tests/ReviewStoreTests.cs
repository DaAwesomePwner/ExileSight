using ExileSight.Core.Features;
using Xunit;

namespace ExileSight.Features.GearCmp.Tests;

public sealed class ReviewStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "gear-review-" + Guid.NewGuid().ToString("N"));
    private static EquippedGear Gear(string type, decimal life) => new(type, new() { ["Life"] = life });
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BowAndQuiverCoexistInEitherOrderAndCompareIndependently(bool quiverFirst)
    {
        var model = new GearModel(new GearStore(directory));
        if (quiverFirst) model.Equip(null, GearSlot.OffHand, Gear("Quivers", 20));
        model.Equip(null, GearSlot.MainHand, Gear("Bows", 10));
        if (quiverFirst) Assert.True(model.Has(GearSlot.OffHand));
        model.Equip(null, GearSlot.OffHand, Gear("Quivers", 20));
        model = new(new GearStore(directory));
        Assert.True(model.Has(GearSlot.MainHand));
        Assert.True(model.Has(GearSlot.OffHand));
        Assert.Equal(5, GearStats.Delta(Gear("Bows", 15), model.Replaced(GearSlot.MainHand, Gear("Bows", 15)))["Life"]);
        Assert.Equal(5, GearStats.Delta(Gear("Quivers", 25), model.Replaced(GearSlot.OffHand, Gear("Quivers", 25)))["Life"]);
    }
    [Theory]
    [InlineData("Two Hand Swords")]
    [InlineData("One Hand Swords")]
    public void QuiversNeverPairWithNonBows(string weapon)
    {
        var model = new GearModel(new GearStore(directory));
        model.Equip(null, GearSlot.MainHand, Gear(weapon, 10));
        Assert.Empty(model.Replaced(GearSlot.OffHand, Gear("Quivers", 20)));
        model.Equip(null, GearSlot.OffHand, Gear("Quivers", 20));
        Assert.False(model.Has(GearSlot.MainHand));
        model.Equip(null, GearSlot.MainHand, Gear(weapon, 10));
        Assert.False(model.Has(GearSlot.OffHand));
    }
    [Fact]
    public void BowClearsShieldAndShieldClearsBow()
    {
        var model = new GearModel(new GearStore(directory));
        model.Equip(null, GearSlot.OffHand, Gear("Shields", 10));
        model.Equip(null, GearSlot.MainHand, Gear("Bows", 20));
        Assert.False(model.Has(GearSlot.OffHand));
        model.Equip(null, GearSlot.OffHand, Gear("Shields", 10));
        Assert.False(model.Has(GearSlot.MainHand));
    }
    [Theory]
    [InlineData("default.json")]
    [InlineData("active.json")]
    public void FutureSchemaIsPreservedWithoutQuarantineAndCannotBeOverwritten(string file)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, file);
        const string text = """{"SchemaVersion":99,"FutureShape":{"keep":"unchanged"}}""";
        File.WriteAllText(path, text);
        var model = new GearModel(new GearStore(directory));
        Assert.Contains("read-only", model.Notice);
        Assert.Throws<IOException>(() => model.Equip(null, GearSlot.Helmet, Gear("Helmets", 10)));
        if (file == "active.json") Assert.Throws<IOException>(() => model.Confirm(null));
        Assert.Equal(text, File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(directory, "*.corrupt"));
    }
    [Fact]
    public void TransientReadFailurePreservesOriginalAndDisablesWritesUntilRestart()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "default.json");
        const string text = """{"SchemaVersion":1,"Slots":{}}""";
        File.WriteAllText(path, text);
        var store = new GearStore(directory, _ => throw new IOException("sharing violation"));
        var model = new GearModel(store);
        Assert.Contains("session", model.Notice);
        Assert.Throws<IOException>(() => model.Equip(null, GearSlot.Helmet, Gear("Helmets", 10)));
        Assert.Equal(text, File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(directory, "*.corrupt"));
        new GearModel(new GearStore(directory)).Equip(null, GearSlot.Helmet, Gear("Helmets", 10));
    }
    [Fact]
    public void DefaultMenuEntryAppearsOnlyForConfirmedProfile()
    {
        var model = new GearModel(new GearStore(directory));
        var feature = new GearCmpFeature(model);
        var item = StatsTests.Item("Rings");
        Assert.DoesNotContain(feature.BuildMenu(item), e => e.Label == "Use default profile");
        model.Observe("Owner"); model.Confirm("Owner");
        Assert.Contains(feature.BuildMenu(item), e => e.Label == "Use default profile");
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
