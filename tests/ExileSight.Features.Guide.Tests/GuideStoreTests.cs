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
    public void VisibilityChangesNotifyWithoutPolling()
    {
        var model = new GuideModel();
        var notifications = new List<bool>();
        model.VisibilityChanged += () => notifications.Add(model.Visible);
        model.Toggle(); model.Hide(); model.Hide(); model.Toggle();
        Assert.Equal([true, false, true], notifications);
    }

    [Fact]
    public void CleanupFailureDoesNotRewriteSuccessfulProgressAndRetriesOnCheckpoint()
    {
        var fail = true; var attempts = 0;
        var store = new GuideStore(directory, file =>
        {
            attempts++;
            if (fail) throw new IOException("synthetic delete failure");
            File.Delete(file);
        });
        var old = store.WriteCustom("old"u8.ToArray());
        var model = new GuideModel(store); model.Load(GuideTests.Data(), "v1");
        model.Navigate(1);
        Assert.Equal(1, new GuideStore(directory).Load()!.Index);
        Assert.DoesNotContain("Could not save", model.Snapshot().Text);
        var path = Path.Combine(directory, "progress.json");
        var marker = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, marker);
        fail = false;
        model.Checkpoint();
        Assert.Equal(marker, File.GetLastWriteTimeUtc(path));
        Assert.False(File.Exists(Path.Combine(directory, old + ".json")));
        Assert.True(attempts >= 2);
    }

    [Fact]
    public void FailedImportSaveCannotRetryCleanupAgainstTheOldCustomId()
    {
        var failDelete = true; var attempts = 0;
        var store = new GuideStore(directory, file =>
        {
            attempts++;
            if (failDelete) throw new IOException("synthetic delete failure");
            File.Delete(file);
        });
        store.WriteCustom("orphan"u8.ToArray());
        var model = new GuideModel(store); model.Load(GuideTests.Data(), "v1");
        model.Import(Encoding.UTF8.GetBytes("[[[\"First areaidSyntheticTown1\"]]]"));
        var oldId = new GuideStore(directory).Load()!.CustomId!;
        Assert.True(attempts > 0); // Earlier cleanup failed after a successful save.

        // Block only progress writes; immutable custom files can still be written.
        var blockedTemp = Path.Combine(directory, "progress.json.tmp");
        Directory.CreateDirectory(blockedTemp);
        var next = Encoding.UTF8.GetBytes("[[[\"Second areaidSyntheticTown1\"]]]");
        var nextId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(next));
        model.Import(next);
        failDelete = false;
        var before = attempts;
        model.Checkpoint();
        Assert.Equal(before, attempts);
        Assert.Equal(next, store.ReadCustom(nextId));
        Assert.NotEmpty(store.ReadCustom(oldId));
        Assert.Equal(oldId, new GuideStore(directory).Load()!.CustomId);

        Directory.Delete(blockedTemp);
        model.Checkpoint();
        Assert.Equal(nextId, new GuideStore(directory).Load()!.CustomId);
        Assert.Equal(next, store.ReadCustom(nextId));
        Assert.False(File.Exists(Path.Combine(directory, oldId + ".json")));
        var resumed = new GuideModel(new GuideStore(directory)); resumed.Load(GuideTests.Data(), "v1");
        Assert.Contains("Second Lioneye's Watch", resumed.Snapshot().Text);
    }

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
