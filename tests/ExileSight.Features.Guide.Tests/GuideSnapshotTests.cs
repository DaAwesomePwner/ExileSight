using ExileSight.Features.Guide;
using ExileSight.Core.Logs;
using System.Text;
using ExileSight.Platform.Abstractions.Time;
using Xunit;

namespace ExileSight.Features.Guide.Tests;

public sealed class GuideSnapshotTests
{
    private sealed class Clock : IClock
    {
        public long Milliseconds { get; set; }
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch.AddMilliseconds(Milliseconds);
        public void Delay(int milliseconds) => Milliseconds += milliseconds;
    }

    [Fact]
    public void SnapshotUsesApplicablePagesWithinTheCurrentAct()
    {
        var fixture = GuideTests.Data();
        fixture = fixture with { Pages = [fixture.Pages[0] with { Act = 1 },
            fixture.Pages[1] with { Act = 2 }, fixture.Pages[2] with { Act = 2, Condition = null },
            fixture.Pages[3] with { Act = 2, Condition = new("bandit", ["oak"]) }] };
        var clock = new Clock();
        var model = new GuideModel(clock: clock); model.Load(fixture, "fixture"); model.Toggle();
        model.Navigate(1); clock.Milliseconds = 12000;
        var state = model.StructuredSnapshot();
        Assert.True(state.Visible); Assert.Equal(2, state.Act);
        Assert.Equal(1, state.Step); Assert.Equal(2, state.StepCount); Assert.Equal(.5, state.Progress);
        Assert.Equal(TimeSpan.FromSeconds(12), state.ActTime); Assert.Equal(state.ActTime, state.TotalTime);
        Assert.Equal(fixture.Pages[1].Lines.Select(l => GuideDisplay.Line(l.Text, fixture)), state.Lines);
        Assert.Equal(GuideDisplay.Line(fixture.Pages[2].Lines[0].Text, fixture), state.NextLine);
        model.Navigate(1); model.Navigate(1);
        state = model.StructuredSnapshot();
        Assert.True(state.Complete); Assert.Equal(2, state.Step); Assert.Equal(1, state.Progress);
        Assert.Null(state.NextLine); Assert.Equal("Guide complete", state.Notice);
    }

    [Fact]
    public void WaitingAndResetHaveOneNotice()
    {
        var model = new GuideModel();
        Assert.Null(model.StructuredSnapshot().Act);
        Assert.Contains("Waiting", model.StructuredSnapshot().Notice);
        model.Load(GuideTests.Data(), "fixture"); model.Reset();
        Assert.Equal("Progress and timers reset.", model.StructuredSnapshot().Notice);
    }

    [Fact]
    public void UncertaintyAndStoreNoticesAreExposed()
    {
        var model = new GuideModel(); model.Load(GuideTests.Data(), "fixture");
        model.Accept(GuideTests.Entry(new LogSourceChanged()));
        Assert.Equal("Timing uncertain: log gap/source change.", model.StructuredSnapshot().Notice);
        var directory = Path.Combine(Path.GetTempPath(), "guide-snapshot-" + Guid.NewGuid());
        try
        {
            var store = new GuideStore(directory);
            store.Save(new(2, "old", 0, new long[11], false, false, true, "none", null));
            model = new GuideModel(store); model.Load(GuideTests.Data(), "new");
            Assert.Contains("version changed", model.StructuredSnapshot().Notice);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void CustomGuideAndRemovalUpdateSnapshotPages()
    {
        var model = new GuideModel(); model.Load(GuideTests.Data(), "fixture");
        model.Import(Encoding.UTF8.GetBytes("[[[\"Custom areaidSyntheticTown1\"]]]"));
        var state = model.StructuredSnapshot();
        Assert.Equal(1, state.StepCount); Assert.Single(state.Lines);
        Assert.StartsWith("Custom", state.Lines[0]); Assert.Null(state.NextLine);
        model.RemoveCustom();
        Assert.DoesNotContain("Custom", model.StructuredSnapshot().Lines[0]);
    }

    [Fact]
    public void FailedCustomWriteLeavesSnapshotOnOriginalGuide()
    {
        var directory = Path.Combine(Path.GetTempPath(), "guide-snapshot-" + Guid.NewGuid());
        try
        {
            var model = new GuideModel(new GuideStore(directory)); model.Load(GuideTests.Data(), "fixture");
            var before = model.StructuredSnapshot();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            File.WriteAllText(directory, "blocked directory");
            Assert.ThrowsAny<IOException>(() => model.Import(Encoding.UTF8.GetBytes("[[[\"Custom areaidSyntheticTown1\"],[\"Next areaidSyntheticTown1\"]]]")));
            var after = model.StructuredSnapshot();
            Assert.Equal(before.StepCount, after.StepCount); Assert.Equal(before.Lines, after.Lines);
            Assert.Equal(before.NextLine, after.NextLine);
        }
        finally
        {
            if (File.Exists(directory)) File.Delete(directory);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
