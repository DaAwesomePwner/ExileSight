using System.Text.Json;
using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class ActionDataTests
{
    [Theory]
    [InlineData(false, "17D 67D 67U 17U")]
    [InlineData(true, "17D 18D 67D 67U 18U 17U")]
    public void CopySequence(bool advanced, string expected) =>
        Assert.Equal(expected, Format(new GameAction.CopyItem(advanced)));
    [Fact]
    public void ChatSequenceIsOneMessage() =>
        Assert.Equal("13D 13U 17D 86D 86U 17U 13D 13U", Format(new GameAction.SendChatCommand("/hideout")));
    private static string Format(GameAction action) =>
        string.Join(" ", ActionData.Sequence(action).Select(k => $"{k.Key}{(k.Down ? "D" : "U")}"));
    [Theory]
    [InlineData("/hideout", "/hideout")]
    [InlineData("/w Person secret words", "/w")]
    [InlineData("@Person secret words", null)]
    [InlineData("secret message", null)]
    [InlineData("/invalid-secret", null)]
    public void AuditNeverIncludesChatArguments(string text, string? verb)
    {
        var date = DateTimeOffset.UnixEpoch;
        var line = ActionData.Audit(date, "hotkey\nforged", new GameAction.SendChatCommand(text), ActionOutcome.NotForeground);
        using var json = JsonDocument.Parse(line);
        Assert.Equal(verb, json.RootElement.GetProperty("verb").GetString());
        Assert.Equal("NotForeground", json.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("SendChatCommand", json.RootElement.GetProperty("action").GetString());
        Assert.Equal(date, json.RootElement.GetProperty("timestamp").GetDateTimeOffset());
        Assert.DoesNotContain("secret", line);
        Assert.DoesNotContain("\n", line);
    }
}
