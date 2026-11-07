using ExileSight.Core.Logs;
using Xunit;

namespace ExileSight.Core.Tests.Logs;

public sealed class PolishParserTests
{
    [Theory]
    [InlineData("Connecting to instance server at 1.2.3.4:6112")]
    [InlineData("Connected to 1.2.3.4:6112 in 42ms.")]
    [InlineData("Connecting to login server at login.example:20481")]
    public void ClientDiagnosticsAreNotLocalChat(string text)
    {
        var e = new LogParser().Parse("2026/11/06 12:00:00 123 abc [INFO Client 42] " + text);
        Assert.IsType<Unknown>(e);
        Assert.Equal(text, e.Envelope!.Message);
        Assert.IsType<ChatMessage>(new LogParser().Parse("Bob: " + text));
    }

    [Theory]
    [InlineData("Test. Thanks in advance!")]
    [InlineData("Test (please invite)")]
    [InlineData("Test")]
    public void LeagueEndsAtFirstTerminator(string suffix)
    {
        var e = Assert.IsType<TradeWhisper>(new LogParser().Parse("@From Peer: I'd like to buy your 10 Chaos Orb for my 1 Divine Orb in " + suffix));
        Assert.Equal("Test", e.League);
        Assert.Equal("10 Chaos Orb", e.Item);
        Assert.Equal("1 Divine Orb", e.Price);
    }
}
