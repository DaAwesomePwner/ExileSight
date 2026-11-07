using ExileSight.Core.Input;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class ProductionHotkeyTests
{
    private sealed class FakeCommand : IFeatureCommandHandler
    {
        public string Command => "overlay.toggle";
        public int Calls { get; private set; }
        public ActionResult Execute() { Calls++; return new(ActionOutcome.Success); }
    }
    private static PersistedBinding Binding() => new() { Id = "copy", Key = 118, Action = ActionKind.CopyItem };
    private static PhysicalKey Key(bool down = true) => new(118, down, false, 0, 10000);

    [Fact]
    public void DefaultsPreserveSpikeActions()
    {
        var mapped = BindingMapper.Map(new SettingsDocument().Hotkeys, new(), []);
        Assert.Empty(mapped.Rejected);
        Assert.IsType<GameAction.CopyItem>(mapped.Bindings[0].Action);
        Assert.Equal(new GameAction.SendChatCommand("/hideout"), mapped.Bindings[1].Action);
        Assert.All(mapped.Bindings, b => Assert.True(b.Block));
    }
    [Theory]
    [InlineData("context")]
    [InlineData("key")]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("modifiers")]
    [InlineData("text")]
    [InlineData("newline")]
    [InlineData("action")]
    [InlineData("feature")]
    public void InvalidBindingsAreRejected(string field)
    {
        var b = Binding();
        switch (field)
        {
            case "context": b.Context = "unknown"; break;
            case "key": b.Key = 256; break;
            case "left": b.Key = 1; break;
            case "right": b.Key = 2; break;
            case "modifiers": b.Modifiers = (KeyModifiers)16; break;
            case "text": b.Action = ActionKind.SendChatCommand; break;
            case "newline": b.Action = ActionKind.SendChatCommand; b.Text = "/hideout\n/help"; break;
            case "action": b.Action = (ActionKind)42; break;
            case "feature": b.Action = null; b.FeatureCommand = "missing"; break;
        }
        var mapped = BindingMapper.Map([b], new(), []);
        Assert.Empty(mapped.Bindings);
        Assert.Single(mapped.Rejected);
    }
    [Fact]
    public void DuplicateChordsAreRejectedOnlyWithinContext()
    {
        var contexts = new HotkeyConfiguration();
        contexts.Register("overlay");
        var second = Binding(); second.Id = "second";
        var third = Binding(); third.Id = "third"; third.Context = "overlay";
        var mapped = BindingMapper.Map([Binding(), second, third], contexts, []);
        Assert.Equal(2, mapped.Bindings.Count);
        Assert.Single(mapped.Rejected);
    }
    [Fact]
    public void OverlayContextAndReloadPreserveHeldStateAndQueuedBinding()
    {
        var contexts = new HotkeyConfiguration();
        contexts.Register("overlay");
        var b = Binding(); b.Context = "overlay";
        var mapped = BindingMapper.Map([b], contexts, []);
        contexts.Replace(mapped.Bindings, mapped.Rejected);
        var dispatcher = new HotkeyDispatcher(new(new FakeClock()), contexts);
        Assert.Null(dispatcher.Observe(Key(), true).Press);
        contexts.SetActive("overlay", true);
        Assert.Null(dispatcher.Observe(Key(), true).Press);
        dispatcher.Observe(Key(false), true);
        var press = dispatcher.Observe(Key(), true);
        Assert.True(press.Block);
        contexts.Replace([], []);
        Assert.True(dispatcher.Observe(Key(), true).Block);
        Assert.True(dispatcher.Observe(Key(false), true).Block);
        Assert.Null(dispatcher.Observe(Key(), true).Press);
        Assert.Equal("copy", press.Binding!.Id);
        Assert.NotNull(dispatcher.Issue(press.Press!));
    }
    [Fact]
    public void FeatureCommandsMapWithoutGameActionAuthority()
    {
        var b = Binding(); b.Action = null; b.FeatureCommand = "overlay.toggle";
        var handler = new FakeCommand();
        var mapped = BindingMapper.Map([b], new(), [handler]);
        Assert.Empty(mapped.Rejected);
        Assert.Null(mapped.Bindings[0].Action);
        var execution = new HotkeyExecution(null!, [handler]);
        Assert.Equal(ActionOutcome.Success, execution.Execute(null, mapped.Bindings[0]).Outcome);
        Assert.Equal(1, handler.Calls);
        var dispatcher = new HotkeyDispatcher(new(new FakeClock()), mapped.Bindings);
        var press = dispatcher.Observe(Key(), true).Press!;
        Assert.Throws<InvalidOperationException>(() => dispatcher.Issue(press));
    }
    [Theory]
    [InlineData(0x207, 0, 4)]
    [InlineData(0x20B, 0x10000, 5)]
    [InlineData(0x20B, 0x20000, 6)]
    public void MouseUsesSameBlockingRepeatAndInjectionPolicy(uint message, uint data, int vk)
    {
        var dispatcher = new HotkeyDispatcher(new(new FakeClock()),
            [new("mouse", vk, 0, true, new GameAction.CopyItem())]);
        var physical = MouseButtonEvent.Decode(message, data, false, 0, 10000)!.Value;
        Assert.Equal(vk, physical.Key);
        Assert.Equal(default, dispatcher.Observe(physical with { Injected = true }, true));
        Assert.NotNull(dispatcher.Observe(physical, true).Press);
        Assert.Null(dispatcher.Observe(physical, true).Press);
        Assert.False(dispatcher.Observe(physical with { Down = false, Injected = true }, true).Block);
        Assert.True(dispatcher.Observe(MouseButtonEvent.Decode(message + 1, data, false, 0, 10000)!.Value, true).Block);
    }
    [Fact]
    public void LeftRightAndWheelAreNeverMouseHotkeys()
    {
        foreach (var message in new uint[] { 0x201, 0x202, 0x204, 0x205, 0x20A })
            Assert.Null(MouseButtonEvent.Decode(message, 0, false, 0, 0));
    }
    [Fact]
    public async Task ConcurrentReloadNeverExposesPartialTable()
    {
        var configuration = new HotkeyConfiguration();
        configuration.UpdateGameFocused(true);
        HotkeyBinding a = new("a", 118, 0, true, new GameAction.CopyItem());
        HotkeyBinding b = a with { Id = "b", Block = false };
        configuration.Replace([a], []);
        var dispatcher = new HotkeyDispatcher(new(new FakeClock()), configuration);
        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++) configuration.Replace([i % 2 == 0 ? a : b], []);
        });
        for (var i = 0; i < 1000; i++)
        {
            var decision = dispatcher.Observe(Key(), true);
            Assert.NotNull(decision.Press);
            Assert.Equal(decision.Binding!.Id == "a", decision.Block);
            dispatcher.Observe(Key(false), true);
        }
        await writer;
    }
}
