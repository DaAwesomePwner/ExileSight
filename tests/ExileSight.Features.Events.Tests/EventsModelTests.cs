using ExileSight.Core.DataPacks;
using ExileSight.Core.Logs;
using ExileSight.Features.Events;
using Xunit;
using static ExileSight.Features.Events.Tests.EventTrackerTests;

namespace ExileSight.Features.Events.Tests;

public sealed class EventsModelTests
{
    internal static EventDefinition[] Definitions =>
    [new("MercenaryEncounter", "Mercenary encounter", true, "encounter"),
     new("Seer", "Nameless Seer", true, "encounter"),
     new("ReflectingMist", "Reflecting Mist", true, "encounter"),
     new("Hideout", "Discoverable hideout", false, "diagnostic")];

    [Fact]
    public void DefaultsOverridesAndMissingPacksControlToastsWithoutReplayingDisabledEvents()
    {
        var model = new EventsModel();
        var events = Fixture();
        model.Configure(Definitions, id => id == "Seer" ? false : null);
        foreach (var e in events) model.Accept(Delivery(e));
        Assert.Equal("Mercenary encounter", model.Current?.DisplayName);
        Assert.Equal(1, model.PendingCount);
        Assert.False(model.Options.Single(o => o.Event.Id == "Hideout").Enabled);
        model.SetEnabled("Seer", true);
        model.Accept(Delivery(events[2]));
        Assert.Equal(1, model.PendingCount);
        model.Configure(null, _ => null);
        Assert.Null(model.Current);
        Assert.Empty(model.Options);
    }

    [Theory]
    [InlineData("MercenaryEncounter", 1)]
    [InlineData("Seer", 2)]
    [InlineData("ReflectingMist", 3)]
    [InlineData("Hideout", 4)]
    public void EveryEventCanBeDisabledWithoutReplayingOnEnable(string id, int index)
    {
        var model = new EventsModel();
        model.Configure(Definitions, _ => true);
        model.SetEnabled(id, false);
        var events = Fixture();
        model.Accept(Delivery(events[0]));
        model.Accept(Delivery(events[index]));
        Assert.Null(model.Current);
        model.SetEnabled(id, true);
        model.Accept(Delivery(events[index]));
        Assert.Null(model.Current);
        model.Accept(Delivery(new AreaGenerated("", null, 83, "MapWorldsBeach", 101)));
        model.Accept(Delivery(events[index]));
        Assert.Equal(id, model.Current?.Id);
        model.Accept(Delivery(events[index]));
        Assert.Equal(0, model.PendingCount);
    }

    [Fact]
    public void QueueIsBoundedSerialAndExpiresWithoutExtendingCurrentToast()
    {
        long now = 0;
        var model = new EventsModel(() => now);
        model.Configure(Definitions, _ => true);
        var events = Fixture();
        foreach (var e in events) model.Accept(Delivery(e));
        Assert.Equal("MercenaryEncounter", model.Current?.Id);
        Assert.Equal(3, model.PendingCount);
        now = 4999;
        Assert.Equal("MercenaryEncounter", model.Current?.Id);
        now = 5000;
        Assert.Equal("Seer", model.Current?.Id);
        now = 10000;
        Assert.Equal("ReflectingMist", model.Current?.Id);
        now = 15000;
        Assert.Null(model.Current); // Pending hideout is now stale, rather than shown late.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiscontinuityClearsVisibleAndQueuedNotifications(bool source)
    {
        var model = new EventsModel();
        model.Configure(Definitions, _ => true);
        var events = Fixture();
        foreach (var e in events) model.Accept(Delivery(e));
        model.Accept(Delivery(source ? new LogSourceChanged() : events[2], dropped: source ? 0 : 1));
        Assert.Null(model.Current);
        Assert.Equal(0, model.PendingCount);
        model.Accept(Delivery(events[0], replay: true));
        model.Accept(Delivery(events[2]));
        Assert.Null(model.Current);
        model.Accept(Delivery(events[0]));
        model.Accept(Delivery(events[2]));
        if (source) Assert.Equal("Seer", model.Current?.Id);
        else Assert.Null(model.Current); // Recovering the same source must not replay an already seen event.
        model.Accept(Delivery(new AreaGenerated("", null, 83, "MapWorldsBeach", 102)));
        model.Accept(Delivery(events[2]));
        Assert.Equal("Seer", model.Current?.Id);
    }

    [Fact]
    public void DisablingActiveOrQueuedEventImmediatelyRemovesItAndPersistsChoice()
    {
        var writes = new List<(string, bool)>();
        var model = new EventsModel(save: (id, enabled) => writes.Add((id, enabled)));
        model.Configure(Definitions, _ => true);
        foreach (var e in Fixture()) model.Accept(Delivery(e));
        model.SetEnabled("Seer", false);
        model.SetEnabled("MercenaryEncounter", false);
        Assert.Equal("ReflectingMist", model.Current?.Id);
        Assert.Equal(1, model.PendingCount);
        Assert.Equal(new[] { ("Seer", false), ("MercenaryEncounter", false) }, writes);
    }
}
