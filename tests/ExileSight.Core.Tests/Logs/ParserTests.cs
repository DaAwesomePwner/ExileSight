using ExileSight.Core.Logs;
using ExileSight.Core.Logs.Redaction;
using Xunit;

namespace ExileSight.Core.Tests.Logs;

public sealed class ParserTests
{
    [Theory]
    [InlineData("campaign", "AreaEntered,CharacterLevelUp,AreaEntered,CharacterLevelUp,AreaEntered,CharacterLevelUp")]
    [InlineData("mapping", "AreaGenerated,AreaEntered,PlayerSlain,PlayerDied,NpcDialogue,NpcDialogue,MercenaryEncounter,MapEvent,AreaEntered")]
    [InlineData("hideout-trade", "AreaEntered,TradeWhisper,ChatMessage,FilterReloaded,ChatMessage,AfkModeChanged,DndChanged")]
    public void Replay(string fixture, string expected)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExileSight.slnx"))) directory = directory.Parent;
        var lines = File.ReadAllLines(Path.Combine(directory!.FullName, "tests/fixtures/poe1/logs", fixture + ".txt"));
        Assert.Equal(expected, string.Join(',', lines.Where(x => !x.StartsWith('#')).Select(x => new LogParser().Parse(x).GetType().Name)));
    }

    [Fact]
    public void EnvelopeAndNumbers()
    {
        var e = Assert.IsType<AreaGenerated>(new LogParser().Parse("\uFEFF2026/11/06 12:00:00 123 abc [INFO Client 42] Generating level 83 area \"MapWorldsBeach\" with seed 4294967295"));
        Assert.Equal(4294967295L, e.Seed);
        Assert.Equal(123, e.Envelope!.UptimeMilliseconds);
        Assert.Equal("abc", e.Envelope.Id);
        Assert.Equal("Client 42", e.Envelope.Source);
    }

    [Theory]
    [InlineData(": 雪 Runner (Witch) is now level 22", typeof(CharacterLevelUp))]
    [InlineData(": Player has been slain.", typeof(PlayerSlain))]
    [InlineData("#<Guild> Snow Runner: hi", typeof(ChatMessage))]
    [InlineData("@To 雪 Runner: Hi, I would like to buy your Ring listed for 1 chaos in Test (stash tab \"S\"; position: left 1, top 2)", typeof(TradeWhisper))]
    [InlineData("garbage", typeof(Unknown))]
    [InlineData(": Bad (Witch) is now level 99999999999999999999", typeof(Unknown))]
    [InlineData("#Imposter: You have entered Beach.", typeof(ChatMessage))]
    public void Events(string line, Type type) => Assert.IsType(type, new LogParser().Parse(line));

    [Fact]
    public void RedactsConsistentlyAndRemovesWhispers()
    {
        var r = new LogRedactor("Owner");
        Assert.Equal("@From PlayerA: [whisper removed]", r.Redact("@From <Guild> Other: secret"));
        Assert.Equal("@To PlayerA: [whisper removed]", r.Redact("@To Other: another secret"));
        Assert.Equal(": Owner (Witch) is now level 4", r.Redact(": Owner (Witch) is now level 4"));
        Assert.Equal(": PlayerB has been slain.", r.Redact(": Third has been slain."));
        Assert.DoesNotContain("Owner", new LogRedactor().Redact(": Owner (Witch) is now level 4"));
    }
}
