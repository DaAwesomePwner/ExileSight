using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class DeferredPressTests
{
    [Theory]
    [InlineData(ActionOutcome.Success)]
    [InlineData(ActionOutcome.Expired)]
    [InlineData(ActionOutcome.NotForeground)]
    [InlineData(ActionOutcome.Failed)]
    [InlineData(null)]
    public void PendingDownIsAcceptedAndReleaseReportsOnlyItsActualResult(ActionOutcome? outcome)
    {
        var clock = new TestClock();
        var dispatcher = new HotkeyDispatcher(TestIntents.Create(clock), []);
        TestIntents.Authorize(dispatcher);
        ActionResult? completed = null;
        var actual = outcome is { } value ? new ActionResult(value, AuditFailed: true, PasteStatus: "actual") : null;
        var releases = 0;
        var router = new PressRouter(dispatcher, clock, () => true, (_, _) => throw new InvalidOperationException(),
            (_, _) => PressRouter.DeferUntilRelease(() => completed),
            (_, _) => { releases++; completed = actual; });
        var down = new PhysicalPress(clock.Milliseconds, new("deferred", 0x74, 0, true, new GameAction.SendChatCommand("/hideout")));
        var pending = router.Route(down);
        Assert.False(router.ShouldReport(down, pending));
        var up = new PhysicalPress(clock.Milliseconds + 100, down.Binding) { Origin = down };
        var result = router.Route(up);
        Assert.Equal(1, releases);
        Assert.Equal(actual is not null, router.ShouldReport(up, result));
        if (actual is not null) Assert.Same(actual, result);
        router.Route(up);
        Assert.Equal(1, releases);
    }
    [Fact]
    public void OrdinarySuccessIsStillReportedOnDownOnly()
    {
        var clock = new TestClock();
        var dispatcher = new HotkeyDispatcher(TestIntents.Create(clock), []);
        TestIntents.Authorize(dispatcher);
        var router = new PressRouter(dispatcher, clock, () => true, (_, _) => new(ActionOutcome.Success));
        var down = new PhysicalPress(clock.Milliseconds, new("copy", 0x74, 0, true, new GameAction.CopyItem()));
        Assert.True(router.ShouldReport(down, router.Route(down)));
        var up = new PhysicalPress(clock.Milliseconds + 100, down.Binding) { Origin = down };
        Assert.False(router.ShouldReport(up, router.Route(up)));
    }
}
