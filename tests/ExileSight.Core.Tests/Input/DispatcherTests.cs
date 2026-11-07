using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class DispatcherTests
{
    private static HotkeyDispatcher Create(bool block = true, KeyModifiers modifiers = 0) =>
        new(TestIntents.Create(new FakeClock()), [new("test", 118, modifiers, block, new GameAction.CopyItem())]);
    private static PhysicalKey Key(bool down = true, bool injected = false, KeyModifiers modifiers = 0) =>
        new(118, down, injected, modifiers, 10000);
    [Fact]
    public void StartupHeldKeysRequireReleaseBeforeIssuing()
    {
        var dispatcher = Create();
        dispatcher.SeedHeld(118);
        Assert.Null(dispatcher.Observe(Key(), true).Press);
        Assert.False(dispatcher.Observe(Key(false), true).Block);
        Assert.NotNull(dispatcher.Observe(Key(), true).Press);
    }
    [Fact]
    public void HeldKeyIssuesOnceUntilRealRelease()
    {
        var dispatcher = Create();
        var first = dispatcher.Observe(Key(), true);
        Assert.True(first.Block);
        Assert.NotNull(dispatcher.IssueForTest(first.Press!));
        Assert.Throws<InvalidOperationException>(() => dispatcher.IssueForTest(first.Press!));
        for (var i = 0; i < 100; i++)
        {
            var repeat = dispatcher.Observe(Key(), true);
            Assert.True(repeat.Block);
            Assert.Null(repeat.Press);
        }
        Assert.True(dispatcher.Observe(Key(false), false).Block);
        Assert.NotNull(dispatcher.Observe(Key(), true).Press);
    }
    [Fact]
    public void InjectedDownAndUpNeitherIssueNorResetPhysicalState()
    {
        var dispatcher = Create();
        Assert.Null(dispatcher.Observe(Key(injected: true), true).Press);
        Assert.NotNull(dispatcher.Observe(Key(), true).Press);
        Assert.False(dispatcher.Observe(Key(false, true), true).Block);
        Assert.Null(dispatcher.Observe(Key(), true).Press);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BindingControlsBlockAndRelease(bool block)
    {
        var dispatcher = Create(block);
        var down = dispatcher.Observe(Key(), true);
        Assert.Equal(block, down.Block);
        Assert.NotNull(down.Press);
        Assert.Equal(block, dispatcher.Observe(Key(false), true).Block);
    }
    [Fact]
    public void ContextTransitionWhileHeldDoesNotCreatePress()
    {
        var dispatcher = Create();
        Assert.Null(dispatcher.Observe(Key(), false).Press);
        Assert.Null(dispatcher.Observe(Key(), true).Press);
        dispatcher.Observe(Key(false), true);
        Assert.NotNull(dispatcher.Observe(Key(), true).Press);
    }
    [Theory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Windows)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Alt)]
    public void ModifiersMustMatchExactly(KeyModifiers modifiers)
    {
        var dispatcher = Create(modifiers: modifiers);
        Assert.Null(dispatcher.Observe(Key(), true).Press);
        dispatcher.Observe(Key(false), true);
        Assert.NotNull(dispatcher.Observe(Key(modifiers: modifiers), true).Press);
    }
    [Fact]
    public void UnboundAndInvalidKeysPassThrough()
    {
        var dispatcher = Create();
        Assert.Equal(default, dispatcher.Observe(new(-1, true, false, 0, 1), true));
        Assert.False(dispatcher.Observe(new(119, true, false, 0, 1), true).Block);
        Assert.Null(dispatcher.Observe(new(119, true, false, 0, 1), true).Press);
    }
}
