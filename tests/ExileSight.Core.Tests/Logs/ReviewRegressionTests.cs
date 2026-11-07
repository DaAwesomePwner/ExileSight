using System.Runtime.CompilerServices;
using ExileSight.Core.Logs;
using ExileSight.Core.Logs.Redaction;
using ExileSight.Platform.Abstractions.Files;
using Xunit;

namespace ExileSight.Core.Tests.Logs;

public sealed class ReviewRegressionTests
{
    [Theory]
    [InlineData("RealName: Have we met before?")]
    [InlineData("Bob: X (Witch) is now level 90")]
    [InlineData("<GUILD> Bob: X has been slain.")]
    [InlineData("Bob: X has committed suicide.")]
    [InlineData("#Bob: The Nameless Seer has appeared nearby")]
    [InlineData("Bob: Finished reloading online filter secret")]
    [InlineData("Bob: Generating level 83 area \"MapWorldsBeach\" with seed 1")]
    public async Task SpeakerLinesCannotSpoofEvents(string message)
    {
        var line = "2026/11/06 12:00:00 123 abc [INFO Client 42] " + message;
        Assert.IsType<ChatMessage>(new LogParser().Parse(line));
        var stream = new LogEventStream(new Lines(line), new LogParser());
        await foreach (var e in stream.ReadAsync("unused")) Assert.IsType<ChatMessage>(e);
        Assert.Null(stream.LastKnownCharacter);
        Assert.Null(stream.LastKnownArea);
        var redacted = new LogRedactor().Redact(line);
        Assert.DoesNotContain("RealName", redacted);
        Assert.DoesNotContain("Bob", redacted);
        Assert.DoesNotContain("GUILD", redacted);
    }

    [Theory]
    [InlineData("Cyaxan")]
    [InlineData("Keitan")]
    [InlineData("Azadin")]
    [InlineData("Bardiyan")]
    public void MercenaryRequiresTitle(string title)
    {
        var speaker = "Aven, the " + title;
        Assert.IsType<MercenaryEncounter>(new LogParser().Parse(speaker + ": Have we met before?"));
        Assert.Equal(speaker + ": [dialogue removed]", new LogRedactor().Redact(speaker + ": Have we met before?"));
        Assert.IsType<MercenaryEncounter>(new LogParser().Parse(speaker + ": Have we met before? Hah!"));
        Assert.IsType<ChatMessage>(new LogParser().Parse(speaker + "_Fake: Have we met before?"));
    }

    [Theory]
    [InlineData("RealName: Have we met before?", "PlayerA: [chat removed]")]
    [InlineData("<GUILD> RealName: hello", "PlayerA: [chat removed]")]
    [InlineData("Guff \"Tiny\" Grenn: secret", "Guff \"Tiny\" Grenn: [dialogue removed]")]
    [InlineData("Einhar, Beastmaster: secret", "Einhar, Beastmaster: [dialogue removed]")]
    [InlineData("Finished reloading online filter AccountName", "Finished reloading filter [name removed]")]
    public void RedactionChecksSpeakerAndRemovesPrivateText(string line, string expected) =>
        Assert.Equal(expected, new LogRedactor().Redact(line));

    [Theory]
    [InlineData("I'd like to buy your 10 Chaos Orb for my 1 Divine Orb in Test", "10 Chaos Orb", "1 Divine Orb", "Test", null)]
    [InlineData("Hi, I'd like to buy your 10 Chaos Orb for my 1 Divine Orb in Test.", "10 Chaos Orb", "1 Divine Orb", "Test", null)]
    [InlineData("Hi, I would like to buy your Ring in Test (stash tab \"S\"; position: left 1, top 2) please", "Ring", null, "Test", "stash tab \"S\"; position: left 1, top 2")]
    [InlineData("Hi, I would like to buy your Ring listed for 1 chaos in Test (stash tab \"S\"; position: left 1, top 2) please", "Ring", "1 chaos", "Test", "stash tab \"S\"; position: left 1, top 2")]
    public void TradeVariants(string text, string item, string? price, string league, string? stash)
    {
        var e = Assert.IsType<TradeWhisper>(new LogParser().Parse("@From Peer: " + text));
        Assert.Equal(text, e.Text);
        Assert.Equal(item, e.Item);
        Assert.Equal(price, e.Price);
        Assert.Equal(league, e.League);
        Assert.Equal(stash, e.Stash);
    }

    [Theory]
    [InlineData(": Alert: The Nameless Seer has appeared nearby!", "Seer")]
    [InlineData(": Alert: A Reflecting Mist has manifested nearby!", "ReflectingMist")]
    public void SystemNotificationsUseContains(string line, string kind) =>
        Assert.Equal(kind, Assert.IsType<MapEvent>(new LogParser().Parse(line)).Kind);

    [Theory]
    [InlineData("X (Witch) is now level 90")]
    [InlineData("X has been slain.")]
    public void UnmarkedPlayerEventsAreUnknown(string line) => Assert.IsType<Unknown>(new LogParser().Parse(line));

    private sealed class Lines(string line) : IFileTail
    {
        public async IAsyncEnumerable<string> ReadLinesAsync(string path, long? position = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return line;
        }
    }
}
