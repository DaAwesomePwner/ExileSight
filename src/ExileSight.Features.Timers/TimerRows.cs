using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ExileSight.Features.Timers;

/// <summary>Dispatcher-owned display rows. Refresh changes text in place so a selector keeps its item.</summary>
public sealed class TimerRows
{
    public ObservableCollection<TimerRow> Items { get; } = [];

    public void Refresh(TimersModel model)
    {
        var snapshot = model.Snapshot();
        var ids = snapshot.Select(r => r.Id).ToHashSet();
        foreach (var old in Items.Where(r => !ids.Contains(r.Id)).ToArray())
            Items.Remove(old);
        foreach (var state in snapshot)
        {
            var row = Items.FirstOrDefault(r => r.Id == state.Id);
            if (row is null) { row = new TimerRow(state.Id); Items.Add(row); }
            var value = model.Value(state.Id);
            row.Text = $"{state.Name} · {(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}" +
                (state.Alerted ? " · ended" : state.IsRunning ? " · running" : " · paused");
        }
    }
}

public sealed class TimerRow(Guid id) : INotifyPropertyChanged
{
    public Guid Id { get; } = id;
    private string text = "";
    public string Text
    {
        get => text;
        internal set
        {
            if (text == value) return;
            text = value;
            PropertyChanged?.Invoke(this, new(nameof(Text)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
