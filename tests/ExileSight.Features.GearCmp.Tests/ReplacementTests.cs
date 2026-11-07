using Xunit;

namespace ExileSight.Features.GearCmp.Tests;

public sealed class ReplacementTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "gear-replacement-" + Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("One Hand Swords", "Shields", "Bows", GearSlot.MainHand, 0, -90, 2, false)]
    [InlineData("Bows", "Quivers", "Shields", GearSlot.OffHand, 70, -20, 2, false)]
    [InlineData("Bows", "Quivers", "One Hand Swords", GearSlot.MainHand, 70, -20, 2, false)]
    [InlineData("One Hand Swords", "Shields", "Quivers", GearSlot.OffHand, 70, -20, 2, false)]
    [InlineData("Bows", "Quivers", "Bows", GearSlot.MainHand, 70, 70, 1, true)]
    [InlineData("Bows", "Quivers", "Two Hand Swords", GearSlot.MainHand, 70, -20, 2, false)]
    [InlineData("One Hand Swords", "Shields", "Staves", GearSlot.MainHand, 70, -20, 2, false)]
    public void PreviewMatchesActualPersistedGearChange(string mainClass, string offClass, string candidateClass,
        GearSlot target, decimal candidateLife, decimal expectedDelta, int replacedCount, bool keepsOther)
    {
        var store = new GearStore(directory);
        var model = new GearModel(store);
        model.Equip(null, GearSlot.MainHand, new(mainClass, new() { ["Life"] = 0 }));
        model.Equip(null, GearSlot.OffHand, new(offClass, new() { ["Life"] = 90 }));
        var candidate = new EquippedGear(candidateClass, new() { ["Life"] = candidateLife });
        var replaced = model.Replaced(target, candidate);
        Assert.Equal(replacedCount, replaced.Length);
        Assert.Equal(expectedDelta, GearStats.Delta(candidate, replaced)["Life"]);
        model.Equip(null, target, candidate);
        var saved = new GearStore(directory).Load(null);
        Assert.Equal(expectedDelta, saved.Values.Sum(g => g.Stats["Life"]) - 90);
        Assert.Equal(candidateClass, saved[target].ItemClass);
        Assert.Equal(keepsOther, saved.ContainsKey(target == GearSlot.MainHand ? GearSlot.OffHand : GearSlot.MainHand));
    }
    [Fact]
    public void OversizedFileNoticeRequiresRemovingOrReducingThePreservedFile()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "default.json");
        var text = new string(' ', 1024 * 1024 + 1);
        File.WriteAllText(path, text);
        var model = new GearModel(new GearStore(directory));
        Assert.Contains("file kept", model.Notice);
        Assert.Contains("removed or reduced", model.Notice);
        Assert.DoesNotContain("Restart to retry", model.Notice);
        Assert.Throws<IOException>(() => model.Equip(null, GearSlot.Helmet, new("Helmets", new())));
        Assert.Equal(text, File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(directory, "*.corrupt"));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
