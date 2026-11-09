using System.Windows;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.App.Ui;

/// <summary>Per-surface settings subscription, owned by the overlay window's lifetime.</summary>
public sealed class OverlayTheme(ISettingsStore<SettingsDocument> settings)
{
    public static CardTheme From(OverlaySettings settings) => CardTheme.For(settings.Density, settings.Contrast);
    public CardTheme Current => From(settings.Snapshot.Overlay);

    public void Watch(Window window, Action refresh)
    {
        var previous = Current;
        var closed = false;
        void Changed(object? sender, SettingsChangedEventArgs args) => window.Dispatcher.BeginInvoke(() =>
        {
            if (closed) return;
            var next = Current;
            if (ReferenceEquals(previous, next)) return;
            previous = next;
            refresh();
        });
        settings.Changed += Changed;
        window.Closed += (_, _) => { closed = true; settings.Changed -= Changed; };
    }
}
