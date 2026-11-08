using ExileSight.Core.Logs;
using Xunit;

namespace ExileSight.Core.Tests.Logs;

public sealed class KillCountTests
{
    [Fact]
    public void ParsesSyntheticSystemKillCount()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExileSight.slnx"))) directory = directory.Parent;
        var line = File.ReadAllText(Path.Combine(directory!.FullName, "tests/fixtures/poe1/logs/kill-count-synthetic.txt")).Trim();
        Assert.Equal(12345, Assert.IsType<KillCountReported>(new LogParser().Parse(line)).Count);
    }
    // Synthetic D-044 cases: confirm grouping and singular spelling against a real client line later.
    [Fact]
    public void ParsesSyntheticGroupedAndSingularCounts()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExileSight.slnx"))) directory = directory.Parent;
        var lines = File.ReadAllLines(Path.Combine(directory!.FullName, "tests/fixtures/poe1/logs/kill-count-formats-synthetic.txt"));
        Assert.Equal(new long[] { 12345, 12345, 12345, 1 },
            lines.Select(line => Assert.IsType<KillCountReported>(new LogParser().Parse(line)).Count));
    }
    [Theory]
    [InlineData(": You have killed 12,34 monsters.")]
    [InlineData(": You have killed 1,234.567 monsters.")]
    [InlineData("Someone: You have killed 123 monsters.")]
    [InlineData(": You have killed -1 monsters.")]
    [InlineData(": You have killed 999999999999999999999 monsters.")]
    [InlineData("You have killed 123 monsters.")]
    public void RejectsChatAndInvalidCounts(string line) => Assert.IsNotType<KillCountReported>(new LogParser().Parse(line));
}
