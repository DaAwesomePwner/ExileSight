using System.Collections.Specialized;
using Xunit;

namespace ExileSight.Features.Timers.Tests;

public sealed class EditorRowsTests
{
    [Theory]
    [InlineData(TimerKind.Countdown)]
    [InlineData(TimerKind.Stopwatch)]
    public void RunningAndEditingKeepRowIdentityAndEditorVisibility(TimerKind kind)
    {
        using var folder = new TestFolder();
        var clock = new Clock();
        var model = new TimersModel(new TimerStore(folder.Path), clock);
        var rows = new TimerRows();
        var states = new List<TimersVisibility>();
        model.Toggle();
        model.VisibilityChanged += states.Add;
        var id = model.Add("Selected", kind, TimeSpan.FromSeconds(60), true);
        rows.Refresh(model);
        var selected = Assert.Single(rows.Items);
        var changes = new List<NotifyCollectionChangedAction>();
        rows.Items.CollectionChanged += (_, e) => changes.Add(e.Action);
        var textChanges = 0;
        selected.PropertyChanged += (_, e) => { Assert.Equal("Text", e.PropertyName); textChanges++; };

        model.Start(selected.Id);
        rows.Refresh(model);
        Assert.Contains("running", selected.Text);
        clock.Advance(2);
        model.Tick();
        rows.Refresh(model);
        Assert.Same(selected, Assert.Single(rows.Items));
        Assert.Contains(kind == TimerKind.Countdown ? "00:00:58" : "00:00:02", selected.Text);
        Assert.True(textChanges >= 2);
        Assert.Empty(changes);

        var other = model.Add("Other", kind, TimeSpan.FromSeconds(30), false);
        rows.Refresh(model);
        Assert.Same(selected, rows.Items[0]);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Add }, changes);
        changes.Clear();
        model.Start(other);
        model.Pause(selected.Id);
        rows.Refresh(model);
        Assert.Contains("paused", selected.Text);
        model.Reset(selected.Id);
        model.Rename(selected.Id, "Renamed");
        rows.Refresh(model);
        Assert.Same(selected, rows.Items[0]);
        Assert.Contains("Renamed", selected.Text);
        Assert.Contains(kind == TimerKind.Countdown ? "00:01:00" : "00:00:00", selected.Text);
        Assert.True(model.Snapshot().Single(r => r.Id == other).IsRunning);
        Assert.Empty(changes);

        model.Delete(selected.Id);
        rows.Refresh(model);
        Assert.Equal(other, Assert.Single(rows.Items).Id);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Remove }, changes);
        Assert.Empty(states);
        Assert.True(model.Interactive);
        Assert.True(model.Visible);
    }

    [Fact]
    public void CountdownCompletionUpdatesTheSameRowWithoutClosingEditor()
    {
        using var folder = new TestFolder();
        var clock = new Clock();
        var model = new TimersModel(new TimerStore(folder.Path), clock);
        var rows = new TimerRows();
        model.Toggle();
        var id = model.Add("Tea", TimerKind.Countdown, TimeSpan.FromSeconds(1), true);
        model.Start(id);
        rows.Refresh(model);
        var selected = Assert.Single(rows.Items);
        clock.Advance(2);
        Assert.Single(model.Tick());
        rows.Refresh(model);
        Assert.Same(selected, Assert.Single(rows.Items));
        Assert.Contains("ended", selected.Text);
        Assert.True(model.Interactive);
    }
}
