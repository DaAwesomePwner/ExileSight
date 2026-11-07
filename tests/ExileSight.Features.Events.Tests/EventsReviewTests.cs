using ExileSight.Core.DataPacks;
using ExileSight.Core.Logs;
using ExileSight.Features.Events;
using Xunit;
using static ExileSight.Features.Events.Tests.EventTrackerTests;

namespace ExileSight.Features.Events.Tests;

public sealed class EventsReviewTests
{
    [Fact]
    public void DedupeRetains256InstancesThenEvictsOldestEvenWhenOtherInstancesHaveNoEvents()
    {
        var tracker = new EventTracker();
        void Area(long seed) => tracker.Process(Delivery(new AreaGenerated("", null, 83, "MapWorldsBeach", seed)));
        var seer = new MapEvent("", null, "Seer");
        Area(0);
        Assert.Equal("Seer", tracker.Process(Delivery(seer)));
        for (var seed = 1; seed < 256; seed++) Area(seed);
        Area(0);
        Assert.Null(tracker.Process(Delivery(seer))); // Still retained at exactly 256 instances.
        Area(256);
        Area(0);
        Assert.Equal("Seer", tracker.Process(Delivery(seer))); // Oldest instance was evicted.
        Assert.Null(tracker.Process(Delivery(seer)));
    }

    [Fact]
    public void AreaGeneratedCarryingDroppedBeforeClearsPresentationAndEstablishesFreshInstance()
    {
        var model = new EventsModel();
        model.Configure(EventsModelTests.Definitions, _ => true);
        foreach (var value in Fixture()) model.Accept(Delivery(value));
        Assert.NotNull(model.Current);
        Assert.True(model.PendingCount > 0);
        model.Accept(Delivery(new AreaGenerated("", null, 83, "MapWorldsDunes", 200), dropped: 3));
        Assert.Null(model.Current);
        Assert.Equal(0, model.PendingCount);
        model.Accept(Delivery(new MapEvent("", null, "Seer")));
        Assert.Equal("Seer", model.Current?.Id);
    }

    [Fact]
    public void HideoutBeforeAnyGenerationIsSuppressedAndNotReplayedWhenAreaArrives()
    {
        var model = new EventsModel();
        model.Configure(EventsModelTests.Definitions, _ => true);
        var events = Fixture();
        model.Accept(Delivery(events[4]));
        model.Accept(Delivery(events[0]));
        Assert.Null(model.Current);
        Assert.Equal(0, model.PendingCount);
        model.Accept(Delivery(events[4]));
        Assert.Equal("Hideout", model.Current?.Id);
    }

    [Fact]
    public void ToastQueueDropsNewestAfterFourPendingEntries()
    {
        var queue = new EventToastQueue(() => 0);
        for (var i = 0; i < 7; i++) queue.Enqueue(new EventDefinition(i.ToString(), "Synthetic event", true, "test"));
        Assert.Equal("0", queue.Current?.Id);
        Assert.Equal(4, queue.PendingCount);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(i.ToString(), queue.Current?.Id);
            queue.Remove(i.ToString());
        }
        Assert.Null(queue.Current); // Entries 5 and 6 were rejected, not retained for later display.
        Assert.Equal(0, queue.PendingCount);
    }
}
