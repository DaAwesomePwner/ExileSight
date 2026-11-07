using System.Text.Json;
using ExileSight.App.Features.Macros;
using ExileSight.Core.Features;
using ExileSight.Core.Input;
using ExileSight.Core.Settings;
using ExileSight.Features.Macros;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Window;
using Xunit;

namespace ExileSight.Features.Macros.Tests;

public sealed class PolishTests
{
    [Fact]
    public void MigrationKeepsEightWheelSlotsAndPreservesExplicitCommand0()
    {
        var legacy = new { Commands = MacroSettings.Default.Commands, Destinations = MacroSettings.Default.Destinations };
        var migrated = MacroSettings.Read(JsonSerializer.SerializeToElement(legacy));
        Assert.Equal("/dnd", migrated.Command0);
        Assert.Equal("/dnd", migrated.Commands[0]);
        foreach (var command0 in new[] { "", "my own text" })
        {
            var current = migrated with { Command0 = command0 };
            Assert.Equal(command0, MacroSettings.Read(JsonSerializer.SerializeToElement(current)).Command0);
            Assert.Equal("/dnd", MacroSettings.Read(JsonSerializer.SerializeToElement(current)).Commands[0]);
        }
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"Commands\":[null],\"Destinations\":null}")]
    public void CorruptSettingsShortMacroReleaseNeverSends(string json)
    {
        var feature = new MacrosFeature();
        var sends = 0;
        var host = new FeatureHost([feature], (_, _) => JsonSerializer.Deserialize<JsonElement>(json),
            (_, _, _) => { }, (_, _, _) => { sends++; return new(ActionOutcome.Success); });
        var press = (IPressCommandHandler)host.Commands.Single(c => c.Command == "macros:macro");
        press.Press(new(), new(1, 100));
        press.Release(new(1, 100, 200));
        Assert.Equal(0, sends);
    }
    [Fact]
    public void BackwardsReleaseClearsPendingAndDoesNotOpenOrSend()
    {
        var feature = new MacrosFeature();
        var sends = 0;
        var opens = 0;
        feature.Wheel.Opened += (_, _, _) => opens++;
        var host = new FeatureHost([feature], (_, _) => null, (_, _, _) => { },
            (_, _, _) => { sends++; return new(ActionOutcome.Success); }, a => a());
        var press = (IPressCommandHandler)host.Commands.First();
        press.Press(new(), new(1, 100));
        press.Release(new(1, 100, 99));
        press.Release(new(1, 100, 200));
        Assert.Equal(0, sends + opens);
    }
    [Fact]
    public void EveryRegisteredSlotUsesItsOwnCommandAndOriginalClickToken()
    {
        var feature = new MacrosFeature();
        var config = MacroSettings.Default with { Command0 = "short only",
            Commands = Enumerable.Range(0, 8).Select(i => "wheel " + i).ToArray() };
        var sent = new List<(IntentToken?, string)>();
        var host = new FeatureHost([feature], (_, _) => JsonSerializer.SerializeToElement(config), (_, _, _) => { },
            (_, token, action) => { sent.Add((token, ((GameAction.SendChatCommand)action).Text)); return new(ActionOutcome.Expired); }, a => a());
        var press = (IPressCommandHandler)host.Commands.Single(c => c.Command == "macros:macro");
        for (var i = 0; i < 8; i++)
        {
            press.Press(new(), new(i + 1, i * 1000));
            press.Release(new(i + 1, i * 1000, i * 1000 + 300));
            var command = host.Commands.Single(c => c.Command == "macros:slot" + i);
            Assert.Equal(ActionOutcome.MissingToken, command.Execute().Outcome);
            var click = new IntentToken();
            Assert.Equal(ActionOutcome.Expired, command.Execute(click).Outcome);
            Assert.Equal((click, "wheel " + i), sent[i]);
            Assert.Equal(ActionOutcome.UsedOrUnknownToken, command.Execute(click).Outcome);
        }
        press.Press(new(), new(9, 9000));
        press.Release(new(9, 9000, 9100));
        Assert.Equal("short only", sent[8].Item2);
    }
    [Fact]
    public void ReleaseCapturesCursorBeforeQueuedPresentation()
    {
        var feature = new MacrosFeature();
        var cursor = new WheelOrigin(500, 400);
        feature.Wheel.CursorPosition = () => cursor;
        WheelOrigin? opened = null;
        feature.Wheel.Opened += (_, _, p) => opened = p;
        var queue = new List<Action>();
        var host = new FeatureHost([feature], (_, _) => null, (_, _, _) => { }, dispatch: queue.Add);
        var press = (IPressCommandHandler)host.Commands.First();
        press.Press(new(), new(1, 0));
        press.Release(new(1, 0, 300));
        cursor = new(999, 999);
        queue.Single()();
        Assert.Equal(new WheelOrigin(500, 400), opened);
    }
    [Fact]
    public void EditorTitleUsesMappedRebindingsAndDisabledDefaults()
    {
        var host = new FeatureHost([new MacrosFeature()], (_, _) => null, (_, _, _) => { });
        var settings = new[] {
            new PersistedBinding { Id = "macros.travel", Key = 0x78, Modifiers = KeyModifiers.Control, FeatureCommand = "macros:travel" },
            new PersistedBinding { Id = "macros.macro", Key = 0x75, Enabled = false, FeatureCommand = "macros:macro" } };
        var mapped = BindingMapper.Map(FeatureDefaultBinding.Apply(settings, host.DefaultBindings), new(), host.Commands);
        Assert.Equal("Macros — Control + F9 travel / Unbound commands", MacroBindingLabels.Title(mapped.Bindings));
        mapped = BindingMapper.Map(FeatureDefaultBinding.Apply([], host.DefaultBindings), new(), host.Commands);
        Assert.Equal("Macros — F5 travel / F6 commands", MacroBindingLabels.Title(mapped.Bindings));
    }
}
