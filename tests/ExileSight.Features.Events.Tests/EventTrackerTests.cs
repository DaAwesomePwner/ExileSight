using ExileSight.Core.Logs;
using ExileSight.Features.Events;
using Xunit;

namespace ExileSight.Features.Events.Tests;

public sealed class EventTrackerTests
{
    internal static LogEvent[] Fixture() => File.ReadLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "events-synthetic.txt"))
        .Skip(1).Select(line => new LogParser().Parse(line)).ToArray();
    internal static ClientLogEvent Delivery(LogEvent value, bool replay = false, long dropped = 0, long generation = 0) =>
        new(1, generation, value, replay) { DroppedBefore = dropped };

    [Fact]
    public void FixtureMapsOnlySupportedEventsOncePerInstance()
    {
        var tracker = new EventTracker();
        var events = Fixture();
        Assert.Null(tracker.Process(Delivery(events[1])));
        Assert.Null(tracker.Process(Delivery(events[0])));
        var actual = events.Skip(1).Select(e => tracker.Process(Delivery(e))).OfType<string>().ToArray();
        Assert.Equal(new[] { "MercenaryEncounter", "Seer", "ReflectingMist", "Hideout" }, actual);
        Assert.All(events.Skip(1), e => Assert.Null(tracker.Process(Delivery(e))));
        tracker.Process(Delivery(events[0]));
        Assert.Null(tracker.Process(Delivery(events[1])));
        tracker.Process(Delivery(new AreaGenerated("", null, 83, "MapWorldsBeach", 101)));
        Assert.Equal("MercenaryEncounter", tracker.Process(Delivery(events[1])));
        tracker.Process(Delivery(new AreaGenerated("", null, 83, "MapWorldsDunes", 101)));
        Assert.Equal("MercenaryEncounter", tracker.Process(Delivery(events[1])));
    }

    [Fact]
    public void ReturningToAnInstanceOrRecoveringAGapDoesNotRepeatItsEvents()
    {
        var tracker = new EventTracker();
        var events = Fixture();
        tracker.Process(Delivery(events[0]));
        Assert.Equal("Seer", tracker.Process(Delivery(events[2])));
        tracker.Process(Delivery(new AreaGenerated("", null, 83, "MapWorldsDunes", 200)));
        tracker.Process(Delivery(events[0]));
        Assert.Null(tracker.Process(Delivery(events[2])));
        tracker.Process(Delivery(events[3], dropped: 1));
        tracker.Process(Delivery(events[0]));
        Assert.Null(tracker.Process(Delivery(events[2])));
    }

    [Theory]
    [InlineData("gap")]
    [InlineData("source")]
    [InlineData("generation")]
    public void DiscontinuitiesRequireFreshAreaAndNeverReplay(string kind)
    {
        var tracker = new EventTracker();
        var events = Fixture();
        tracker.Process(Delivery(events[0]));
        Assert.NotNull(tracker.Process(Delivery(events[1])));
        tracker.Process(Delivery(kind == "source" ? new LogSourceChanged() : events[2],
            dropped: kind == "gap" ? 1 : 0, generation: kind == "generation" ? 1 : 0));
        var generation = kind == "generation" ? 1 : 0;
        Assert.Null(tracker.Process(Delivery(events[2], generation: generation)));
        tracker.Process(Delivery(events[0], replay: true, generation: generation));
        Assert.Null(tracker.Process(Delivery(events[2], generation: generation)));
        tracker.Process(Delivery(events[0], generation: generation));
        Assert.Equal("Seer", tracker.Process(Delivery(events[2], generation: generation)));
        Assert.Null(tracker.Process(Delivery(events[3], replay: true, generation: generation)));
        Assert.Equal("ReflectingMist", tracker.Process(Delivery(events[3], generation: generation)));
    }
}
