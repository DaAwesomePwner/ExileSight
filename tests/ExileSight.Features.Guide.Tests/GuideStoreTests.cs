using System.Text;
using ExileSight.Features.Guide;
using Xunit;

namespace ExileSight.Features.Guide.Tests;

public sealed class GuideStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "guide-test-" + Guid.NewGuid());
    private static GuideSave State => new(1, "v1", 1, new long[11], false, false, true, "none", null);
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    [Fact]
    public void RoundTripIsAtomicAndContainsNoCharacterName()
    {
        var store = new GuideStore(directory); store.Save(State);
        var loaded = new GuideStore(directory).Load();
        Assert.Equal(1, loaded!.Index); Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        var model = new GuideModel(store); model.Load(GuideTests.Data(), "v1");
        model.Accept(GuideTests.Entry(new ExileSight.Core.Logs.CharacterLevelUp("Secret raw", null, "PrivateName", "Witch", 9)));
        var json = File.ReadAllText(Path.Combine(directory, "progress.json"));
        Assert.DoesNotContain("PrivateName", json); Assert.DoesNotContain("Secret raw", json);
        Assert.Contains("Single default profile", model.Snapshot().Text);
    }
    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"Schema\":99}")]
    public void CorruptAndFutureDataRemainUntouched(string json)
    {
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "progress.json"); File.WriteAllText(path, json);
        var store = new GuideStore(directory); Assert.Null(store.Load()); store.Save(State);
        Assert.Equal(json, File.ReadAllText(path)); Assert.NotNull(store.Notice);
    }
    [Fact]
    public void InvalidImportDoesNotReplaceProgress()
    {
        var model = new GuideModel(new GuideStore(directory)); model.Load(GuideTests.Data(), "v1"); model.Navigate(1);
        var before = File.ReadAllText(Path.Combine(directory, "progress.json"));
        Assert.Throws<ArgumentException>(() => model.Import(Encoding.UTF8.GetBytes("[[[\"areaidMissing\"]]]")));
        Assert.Equal(before, File.ReadAllText(Path.Combine(directory, "progress.json")));
    }
    [Fact]
    public void CustomGuideSurvivesRestart()
    {
        var model = new GuideModel(new GuideStore(directory)); model.Load(GuideTests.Data(), "v1");
        model.Import(Encoding.UTF8.GetBytes("[[[\"Custom areaidSyntheticTown1\"]]]"));
        var resumed = new GuideModel(new GuideStore(directory)); resumed.Load(GuideTests.Data(), "v1");
        Assert.Contains("Custom Lioneye's Watch", resumed.Snapshot().Text);
    }
    [Fact]
    public void InapplicableConditionChangeDoesNotChangeSavedOptions()
    {
        var model = new GuideModel(new GuideStore(directory)); model.Load(GuideTests.Data(), "v1");
        model.Import(Encoding.UTF8.GetBytes("""[[{"condition":["league-start","yes"],"lines":["areaidSyntheticTown1"]}]]"""));
        Assert.Throws<ArgumentException>(() => model.Reset(changeLeague: true));
        Assert.Contains("League start: yes", model.Snapshot().Text);
    }
    [Fact]
    public void OutOfRangeSavedPageIsPreserved()
    {
        var store = new GuideStore(directory); store.Save(State with { Index = 100 });
        var before = File.ReadAllText(Path.Combine(directory, "progress.json"));
        var model = new GuideModel(store); model.Load(GuideTests.Data(), "v1"); model.Navigate(1);
        Assert.Equal(before, File.ReadAllText(Path.Combine(directory, "progress.json")));
    }
}
