using System.Reflection;
using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class NativeBoundaryLogicTests
{
    [Theory]
    [InlineData("POEWindowClass", 1, "", true)]
    [InlineData("Other", 42, "Path of Exile — GeForce NOW", true)]
    [InlineData("Other", 43, "Path of Exile", false)]
    [InlineData("Other", 42, "Another game", false)]
    [InlineData("Other", 42, "PATH OF EXILE", true)]
    public void ForegroundRequiresNativeClassOrBothGfnPidAndTitle(string cls, uint pid, string title, bool expected) =>
        Assert.Equal(expected, ForegroundRule.Matches(cls, pid, title, new HashSet<uint> { 42 }));
    [Theory]
    [InlineData("item\0garbage", "item")]
    [InlineData("\0", "")]
    public void ClipboardReadStopsAtTerminator(string buffer, string expected) =>
        Assert.Equal(expected, ClipboardText.Decode(buffer));
    [Fact]
    public void UnterminatedClipboardTextIsRejected() =>
        Assert.Throws<InvalidOperationException>(() => ClipboardText.Decode("no terminator"));
    [Fact]
    public void IssuanceAndEventObservationAreNotPublic()
    {
        var methods = typeof(HotkeyDispatcher).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(methods, m => m.Name is "Observe" or "Issue" or "SeedHeld");
        Assert.DoesNotContain(typeof(IntentRegistry).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            m => m.Name == "Issue");
    }
    [Fact]
    public void PartialSendCleanupOnlyReleasesEveryDownKeyOnce()
    {
        var sequence = ActionData.Sequence(new GameAction.SendChatCommand("/hideout"));
        var cleanup = ActionData.ReleaseSequence(sequence);
        Assert.All(cleanup, key => Assert.False(key.Down));
        Assert.Equal(new ushort[] { 86, 17, 13 }, cleanup.Select(k => k.Key));
    }
}
