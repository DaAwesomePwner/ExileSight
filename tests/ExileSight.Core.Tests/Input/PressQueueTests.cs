using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class PressQueueTests
{
    [Fact]
    public void OverflowFailsClosedAndRingWrapPreservesOrder()
    {
        var queue = new PressQueue();
        var dispatcher = new HotkeyDispatcher(TestIntents.Create(new FakeClock()),
            [new("test", 118, 0, true, new GameAction.CopyItem())]);
        for (var round = 0; round < 3; round++)
        {
            var expected = new List<PhysicalPress>();
            for (var i = 0; i < 64; i++)
            {
                var press = dispatcher.Observe(new(118, true, false, 0, i), true).Press!;
                dispatcher.Observe(new(118, false, false, 0, i), true);
                Assert.True(queue.TryEnqueue(press));
                expected.Add(press);
            }
            Assert.False(queue.TryEnqueue(expected[0]));
            foreach (var press in expected)
            {
                Assert.True(queue.TryDequeue(out var actual));
                Assert.Same(press, actual);
            }
            Assert.False(queue.TryDequeue(out _));
        }
    }
}
