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
    [Theory]
    [InlineData("Someone: You have killed 123 monsters.")]
    [InlineData(": You have killed -1 monsters.")]
    [InlineData(": You have killed 999999999999999999999 monsters.")]
    [InlineData("You have killed 123 monsters.")]
    public void RejectsChatAndInvalidCounts(string line) => Assert.IsNotType<KillCountReported>(new LogParser().Parse(line));
}
