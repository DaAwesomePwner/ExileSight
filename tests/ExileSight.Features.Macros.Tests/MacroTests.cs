using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Features.Macros;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Macros.Tests;

public sealed class MacroTests
{
    private sealed class Harness
    {
        public MacrosFeature Feature { get; } = new();
        public List<(IntentToken? Token, string Text)> Attempts { get; } = [];
        public FeatureHost Host { get; }
        public ActionOutcome Outcome { get; set; } = ActionOutcome.Success;
        public JsonElement? Settings { get; set; }
        public Harness(Action<Action>? dispatch = null)
        {
            Host = new([Feature], (_, _) => Settings, (_, _, value) => Settings = value,
                (_, token, action) =>
                {
                    Attempts.Add((token, Assert.IsType<GameAction.SendChatCommand>(action).Text));
                    return new(Outcome);
                }, dispatch ?? (a => a()));
        }
        public IPressCommandHandler Press(string name = "travel") =>
            (IPressCommandHandler)Host.Commands.Single(c => c.Command == "macros:" + name);
    }
    [Theory]
    [InlineData("travel", "/hideout")]
    [InlineData("macro", "/dnd")]
    public void ShortReleaseSpendsOriginalTokenOnce(string name, string expected)
    {
        var h = new Harness();
        var token = new IntentToken();
        var command = h.Press(name);
        command.Press(token, new(1, 100));
        command.Press(new(), new(1, 100));
        Assert.Empty(h.Attempts);
        command.Release(new(2, 100, 200));
        command.Release(new(1, 101, 200));
        Assert.Empty(h.Attempts);
        command.Release(new(1, 100, 300));
        command.Release(new(1, 100, 300));
        Assert.Equal((token, expected), Assert.Single(h.Attempts));
    }
    [Theory]
    [InlineData(201)]
    [InlineData(5000)]
    public void LongReleaseOnlyOpensThenClickSpendsDifferentToken(long duration)
    {
        var h = new Harness();
        long version = 0;
        h.Feature.Wheel.Opened += (id, slots, _) => { version = id; Assert.Equal(8, slots.Count); };
        h.Press().Press(new(), new(1, 100));
        Assert.Empty(h.Attempts); // no hold timer
        h.Press().Release(new(1, 100, 100 + duration));
        Assert.Empty(h.Attempts);
        var click = new IntentToken();
        Assert.Equal(ActionOutcome.Success, h.Feature.Wheel.SelectSlot(1, version, click).Outcome);
        h.Feature.Wheel.SelectSlot(1, version, click);
        Assert.Equal((click, "/menagerie"), Assert.Single(h.Attempts));
    }
    [Fact]
    public void MissingReleaseReplacedAndOldReleaseIgnored()
    {
        var h = new Harness();
        h.Press().Press(new(), new(1, 100));
        h.Press().Press(new(), new(2, 2000));
        h.Press().Release(new(1, 100, 200));
        Assert.Empty(h.Attempts);
        h.Press().Release(new(2, 2000, 2100));
        Assert.Single(h.Attempts);
    }
    [Theory]
    [InlineData(ActionOutcome.Expired)]
    [InlineData(ActionOutcome.NotForeground)]
    [InlineData(ActionOutcome.Failed)]
    public void GateRefusalNeverRetriesOrOpensWheel(ActionOutcome outcome)
    {
        var h = new Harness { Outcome = outcome };
        var opens = 0;
        h.Feature.Wheel.Opened += (_, _, _) => opens++;
        h.Press().Press(new(), new(1, 100));
        h.Press().Release(new(1, 100, 200)); // delayed processing can expire even a short physical press
        h.Press().Release(new(1, 100, 200));
        Assert.Single(h.Attempts);
        Assert.Equal(0, opens);
    }
    [Fact]
    public void QueuedOldPresentationAndStaleSelectionAreRejected()
    {
        var queue = new List<Action>();
        var h = new Harness(queue.Add);
        var opens = 0;
        h.Feature.Wheel.Opened += (_, _, _) => opens++;
        h.Press().Press(new(), new(1, 100));
        h.Press().Release(new(1, 100, 400));
        h.Press("macro").Press(new(), new(2, 500));
        queue.Single()();
        Assert.Equal(0, opens);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, h.Feature.Wheel.SelectSlot(0, 1, new()).Outcome);
        Assert.Empty(h.Attempts);
    }
    [Fact]
    public void DismissAndBlankSlotsCannotSend()
    {
        var h = new Harness();
        long id = 0;
        h.Feature.Wheel.Opened += (version, _, _) => id = version;
        h.Press().Press(new(), new(1, 0));
        h.Press().Release(new(1, 0, 300));
        h.Feature.Wheel.SelectSlot(7, id, new());
        h.Feature.Wheel.Reset();
        h.Feature.Wheel.SelectSlot(0, id, new());
        Assert.Empty(h.Attempts);
    }
    [Fact]
    public void ManifestDefaultsAndSettingsPersistence()
    {
        var h = new Harness();
        h.Feature.Manifest.Validate();
        Assert.Equal(ActionKind.SendChatCommand, Assert.Single(h.Feature.Manifest.GameActions));
        Assert.Equal(new[] { 0x74, 0x75 }, h.Host.DefaultBindings.Select(b => b.Key));
        Assert.All(h.Host.DefaultBindings, b => Assert.True(b.Block));
        var settings = MacroSettings.Default with { Command0 = "Thanks!" };
        h.Feature.Wheel.SaveSettings!(settings);
        h.Press("macro").Press(new(), new(1, 0));
        h.Press("macro").Release(new(1, 0, 10));
        Assert.Equal("Thanks!", Assert.Single(h.Attempts).Text);
    }
}
