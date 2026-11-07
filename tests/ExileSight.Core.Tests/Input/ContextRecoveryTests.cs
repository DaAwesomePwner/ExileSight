using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class ContextRecoveryTests
{
    [Fact]
    public void BuiltInContextCannotBeSetOrRegisteredByServices()
    {
        IHotkeyContexts contexts = new HotkeyConfiguration();
        Assert.Throws<ArgumentException>(() => contexts.Register("gameFocused"));
        Assert.Throws<ArgumentException>(() => contexts.SetActive("gameFocused", true));
    }
    [Fact]
    public void DeactivatingContextKeepsOneBlockedReleaseAndSuppressesHeldRepeat()
    {
        var configuration = new HotkeyConfiguration();
        configuration.Register("overlay");
        configuration.SetActive("overlay", true);
        configuration.Replace([new("copy", 118, 0, true, new GameAction.CopyItem(), "overlay")], []);
        var dispatcher = new HotkeyDispatcher(new(new FakeClock()), configuration);
        Assert.True(dispatcher.Observe(Key(), true).Block);
        configuration.SetActive("overlay", false);
        dispatcher.ReconcileHeld(_ => true);
        Assert.Null(dispatcher.Observe(Key(), true).Press);
        Assert.True(dispatcher.Observe(Key(false), true).Block);
        Assert.False(dispatcher.Observe(Key(false), true).Block);
    }
    [Fact]
    public void LostReleaseRecoversOnForegroundChangeWithoutReissuingHeldKeys()
    {
        var configuration = new HotkeyConfiguration();
        configuration.UpdateGameFocused(true, 1);
        configuration.Replace([new("copy", 118, 0, true, new GameAction.CopyItem())], []);
        var dispatcher = new HotkeyDispatcher(new(new FakeClock()), configuration);
        Assert.NotNull(dispatcher.Observe(Key(), true).Press);
        configuration.UpdateGameFocused(false, 2);
        dispatcher.ReconcileHeld(_ => true);
        Assert.Null(dispatcher.Observe(Key(), true).Press);
        // Release was not delivered during the secure desktop interval. Async state
        // cannot recover a blocked key; a subsequent down needs a gap in hook events.
        configuration.UpdateGameFocused(true, 1);
        dispatcher.ReconcileHeld(_ => false);
        Assert.NotNull(dispatcher.Observe(Key() with
        {
            Timestamp = 10000 + HotkeyDispatcher.BlockedReleaseSilenceMilliseconds + 1
        }, true).Press);
        Assert.True(dispatcher.Observe(Key(false), true).Block);
    }
    [Fact]
    public void ShadowedBindingProducesWarningUntilWinningContextCloses()
    {
        var configuration = new HotkeyConfiguration();
        configuration.UpdateGameFocused(true);
        configuration.Register("overlay");
        configuration.SetActive("overlay", true);
        configuration.Replace([
            new("first", 118, 0, true, new GameAction.CopyItem()),
            new("second", 118, 0, false, new GameAction.CopyItem(), "overlay")], []);
        Assert.Equal("first", configuration.Match(118, 0)!.Id);
        Assert.Contains("second: Shadowed by first", Assert.Single(configuration.RejectedBindings));
        configuration.UpdateGameFocused(false);
        Assert.Empty(configuration.RejectedBindings);
        Assert.Equal("second", configuration.Match(118, 0)!.Id);
    }
    [Theory]
    [InlineData(0x100u)]
    [InlineData(0x101u)]
    [InlineData(0x104u)]
    [InlineData(0x105u)]
    public void KeyboardInjectedFlagAlwaysPassesThrough(uint message)
    {
        Assert.False(NativeHookEvent.KeyboardAllowed(message, 0x10));
        Assert.False(NativeHookEvent.KeyboardAllowed(message, 0x12));
        Assert.True(NativeHookEvent.KeyboardAllowed(message, 0));
    }
    [Theory]
    [InlineData(0x207u)]
    [InlineData(0x208u)]
    [InlineData(0x20Bu)]
    [InlineData(0x20Cu)]
    public void MouseInjectedFlagAlwaysPassesThrough(uint message)
    {
        Assert.False(NativeHookEvent.MouseAllowed(message, 1));
        Assert.False(NativeHookEvent.MouseAllowed(message, 3));
        Assert.True(NativeHookEvent.MouseAllowed(message, 0));
    }
    [Fact]
    public void MouseMoveRejectedBeforeModifiersOrPayloadAreNeeded() =>
        Assert.False(NativeHookEvent.IsMouseButton(0x200));
    private static PhysicalKey Key(bool down = true) => new(118, down, false, 0, 10000);
}
