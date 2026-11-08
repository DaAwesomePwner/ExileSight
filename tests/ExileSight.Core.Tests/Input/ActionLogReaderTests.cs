using ExileSight.Core.Input;
using Xunit;
namespace ExileSight.Core.Tests.Input;

public sealed class ActionLogReaderTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".log");
    private static string Fixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests", "fixtures"))) directory = directory.Parent;
        return File.ReadAllText(Path.Combine(directory!.FullName, "tests", "fixtures", "actions", "mixed.jsonl"));
    }
    [Fact]
    public async Task ReadsNewestFirstAndPreservesOnlyLoggedFields()
    {
        File.WriteAllText(path, Fixture());
        var result = await ActionLogReader.ReadAsync(path);
        Assert.Equal(2, result.Skipped);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("synthetic-unnamed", result.Entries[0].Trigger);
        Assert.Equal("Sent", result.Entries[0].Result);
        Assert.Equal("/hideout", result.Entries[0].Verb);
        Assert.Equal("Omni key", result.Entries[1].Trigger);
        Assert.False(result.Entries[1].Sent);
        Assert.Equal("Game not in front", result.Entries[1].Result);
        Assert.Equal("Copy item", result.Entries[2].Trigger);
        Assert.True(result.Entries[0].Timestamp > result.Entries[2].Timestamp);
    }
    [Fact]
    public void CapsPhysicalLinesAt200AndReadsSharedLargeTail()
    {
        using var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        file.SetLength(ActionLogReader.MaximumBytes * 8L);
        file.Position = file.Length;
        var line = Fixture().Split('\n')[0];
        using (var writer = new StreamWriter(file, leaveOpen: true))
        {
            writer.WriteLine();
            for (var i = 0; i < 250; i++) writer.WriteLine(line);
            writer.WriteLine("partial");
        }
        var result = ActionLogReader.Read(path);
        Assert.True(result.Truncated);
        Assert.Equal(199, result.Entries.Count);
        Assert.Equal(1, result.Skipped);
    }
    [Fact]
    public void MissingFileIsEmpty()
    {
        var snapshot = ActionLogReader.Read(path);
        Assert.Empty(snapshot.Entries);
        Assert.True(snapshot.Missing);
        Assert.Contains("does not exist", snapshot.Note);
    }
    [Fact]
    public void SharingViolationReturnsErrorNote()
    {
        using var locked = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        var snapshot = ActionLogReader.Read(path);
        Assert.Empty(snapshot.Entries);
        Assert.False(snapshot.Missing);
        Assert.Contains("IOException", snapshot.Note);
    }
    [Fact]
    public void UnreadableDirectoryReturnsErrorNote()
    {
        var snapshot = ActionLogReader.Read(Path.GetTempPath());
        Assert.Empty(snapshot.Entries);
        Assert.False(snapshot.Missing);
        Assert.Contains("Could not read", snapshot.Note);
    }
    [Fact]
    public void SharedPathRetainsExistingWriterLocation() => Assert.Equal(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExileSight", "logs", "actions.log"),
        ActionLogPath.Default);
    [Theory]
    [InlineData("[]")]
    [InlineData("{\"timestamp\":false}")]
    [InlineData("{\"timestamp\":\"invalid\"}")]
    public void MalformedFieldsAreSkipped(string line)
    {
        File.WriteAllText(path, line);
        Assert.Equal(1, ActionLogReader.Read(path).Skipped);
    }
    public void Dispose() => File.Delete(path);
}
