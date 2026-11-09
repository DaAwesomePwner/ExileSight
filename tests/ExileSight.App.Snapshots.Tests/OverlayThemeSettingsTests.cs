using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ExileSight.App.Features.MapInfo;
using ExileSight.App.Features.Timers;
using ExileSight.App.Ui;
using ExileSight.Core.Settings;
using ExileSight.Features.MapInfo;
using ExileSight.Features.Timers;
using ExileSight.Platform.Abstractions.Storage;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class OverlayThemeSettingsTests
{
    [WindowsOnlyFact]
    public void MapUsesSavedCompactHighContrastTheme()
    {
#if SNAPSHOT_WINDOWS
        var path = SnapshotRenderer.Render(() =>
        {
            var theme = OverlayTheme.From(CompactHighContrast());
            Assert.Same(CardTheme.For(OverlayDensity.Compact, OverlayContrast.HighContrast), theme);
            Assert.Equal(24, theme.RowHeight);
            Assert.Same(CardTheme.Default.OverlayForeground, theme.TextSecondary);
            var card = MapCardBuilder.Build(new(new MapResult("Strand Map", 82, 46, 28,
                [new("reflect", "Reflect elemental damage", "Monsters", 3)], 16, "rare"), new()), theme);
            Assert.Equal(theme.CardWidth, card.Width);
            Assert.Same(theme.OverlayBorder, Assert.IsType<Border>(card).BorderBrush);
            return card;
        }, "map-card-compact-high-contrast");
        Assert.True(File.Exists(path));
#endif
    }

    [WindowsOnlyFact]
    public void LongLivedTimerCardRebuildsOnlyForThemeChangesAndUnsubscribes()
    {
#if SNAPSHOT_WINDOWS
        var path = SnapshotRenderer.Render(() =>
        {
            var store = new SettingsStore();
            var source = new OverlayTheme(store);
            var window = new Window();
            var surface = new Border();
            var rebuilds = 0;
            void Refresh()
            {
                rebuilds++;
                surface.Child = TimersCardBuilder.Build([
                    new("Garden", TimerKind.Countdown, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(10))], source.Current);
            }
            Refresh();
            source.Watch(window, Refresh);
            store.Update("overlay", s => s.Overlay = CompactHighContrast());
            Drain();
            Assert.Equal(2, rebuilds);
            Assert.Same(CardTheme.For(OverlayDensity.Compact, OverlayContrast.HighContrast), source.Current);
            Assert.Same(source.Current.OverlayBorder, Assert.IsType<Border>(surface.Child).BorderBrush);
            store.Update("prices", _ => { });
            Drain();
            Assert.Equal(2, rebuilds);
            window.Close();
            Assert.Equal(0, store.Subscribers);
            store.Update("overlay", s => s.Overlay = new());
            Drain();
            Assert.Equal(2, rebuilds);
            return surface;
        }, "timers-compact-high-contrast");
        Assert.True(File.Exists(path));
#endif
    }

#if SNAPSHOT_WINDOWS
    private static OverlaySettings CompactHighContrast() => new()
    { Density = OverlayDensity.Compact, Contrast = OverlayContrast.HighContrast };
    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private sealed class SettingsStore : ISettingsStore<SettingsDocument>
    {
        public SettingsDocument Snapshot { get; } = new();
        public bool IsReadOnly => false;
        public bool IsReadOnlyNewerSchema => false;
        public IReadOnlyList<string> Warnings => [];
        public event EventHandler<SettingsChangedEventArgs>? Changed;
        public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
        public void Update(string section, Action<SettingsDocument> update)
        { update(Snapshot); Changed?.Invoke(this, new(section)); }
        public void Flush() { }
        public void Dispose() { }
    }
#endif
}
