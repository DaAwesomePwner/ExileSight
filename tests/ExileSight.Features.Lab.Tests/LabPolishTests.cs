using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace ExileSight.Features.Lab.Tests;

public sealed class LabPolishTests
{
    private static byte[] Fixture() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "valid.json"));

    [Theory]
    [InlineData("Normal\r\n", "Normal")]
    [InlineData(" \uFEFF  uBeR\t\n", "Uber")]
    [InlineData("unknown difficulty", "Normal")]
    [InlineData("\uFEFF \r\n", "Normal")]
    [InlineData("Normal\0", "Normal")]
    public void MarkerIsLenientAndFallbackLoadsNormal(string marker, string expected)
    {
        WithStore((directory, store) =>
        {
            store.Save(Fixture());
            store.Save(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Fixture()).Replace("Normal", "Uber")));
            File.WriteAllText(Path.Combine(directory, "latest.txt"), marker);
            var model = new LabModel(store); model.RestoreLatest();
            Assert.Equal(expected, model.Snapshot().Difficulty);
            Assert.Equal(expected, model.Snapshot().Layout!.Difficulty);
        });
    }

    [Fact]
    public void OversizedMarkerCannotPreventLoadingNormal()
    {
        WithStore((directory, store) =>
        {
            store.Save(Fixture());
            File.WriteAllBytes(Path.Combine(directory, "latest.txt"), new byte[LabImport.MaxBytes + 1]);
            var model = new LabModel(store); model.RestoreLatest();
            Assert.Equal("Normal", model.Snapshot().Difficulty);
            Assert.NotNull(model.Snapshot().Layout);
        });
    }

    [Fact]
    public void MissingLayoutKeepsMarkerDifficultyInViewSnapshot()
    {
        WithStore((directory, store) =>
        {
            store.Save(Fixture());
            File.WriteAllText(Path.Combine(directory, "latest.txt"), "Uber");
            var model = new LabModel(store); model.RestoreLatest();
            Assert.Equal("Uber", model.Snapshot().Difficulty);
            Assert.Null(model.Snapshot().Layout);
            model.Select("Normal"); Assert.NotNull(model.Snapshot().Layout);
            model.Select("Uber"); Assert.Equal("Uber", model.Snapshot().Difficulty);
            Assert.Null(model.Snapshot().Layout);
        });
    }

    [Theory]
    [InlineData("  Aspirant’s\t Trial  ", "aspirant's   TRIAL")]
    [InlineData("Aspirant's Trial", " ASPIRANT‘S\tTRIAL ")]
    [InlineData("Aspirant‘s  Trial", "Aspirant’s Trial")]
    public void ParserAndTrackerNormaliseRoomNames(string imported, string logged)
    {
        var json = JsonNode.Parse(Fixture())!;
        json["rooms"]![3]!["name"] = imported;
        var layout = LabImport.Parse(Encoding.UTF8.GetBytes(json.ToJsonString()));
        Assert.Equal("Aspirant's Trial", layout.Rooms[3].Name);
        var tracker = new LabTracker(layout);
        tracker.Enter("  ASPIRANTS’\t PLAZA "); Assert.True(tracker.AtPlaza); Assert.Null(tracker.Current);
        tracker.Enter(" sanitorium "); tracker.Enter("HALLS"); tracker.Enter("Sanitorium"); tracker.Enter(logged);
        Assert.Equal("d", tracker.Current!.Id); Assert.False(tracker.Uncertain);
        // Hand-constructed models receive the same comparison rules as parsed layouts.
        var direct = new LabTracker(layout with { Rooms = [layout.Rooms[3] with { Name = imported }] });
        direct.Enter(logged); Assert.Equal("d", direct.Current!.Id);
    }

    private static void WithStore(Action<string, LabStore> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try { action(directory, new LabStore(directory)); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
