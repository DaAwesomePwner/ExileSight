using ExileSight.Core.Input;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class HotkeySettingsTests
{
    private sealed class Store : ISettingsStore<SettingsDocument>
    {
        public SettingsDocument Snapshot { get; } = new();
        public bool IsReadOnly => false;
        public bool IsReadOnlyNewerSchema => false;
        public IReadOnlyList<string> Warnings => [];
        public event EventHandler<SettingsChangedEventArgs>? Changed;
        public void Update(string section, Action<SettingsDocument> update)
        {
            update(Snapshot);
            Changed?.Invoke(this, new(section));
        }
        public void Flush() { }
        public void Dispose() { }
    }
    [Fact]
    public void SettingsChangeReplacesBindingsAndDisposeUnsubscribes()
    {
        using var store = new Store();
        var configuration = new HotkeyConfiguration();
        configuration.SetActive("gameFocused", true);
        var settings = new HotkeySettings(store, configuration, []);
        var dispatcher = new HotkeyDispatcher(new(new FakeClock()), configuration);
        Assert.NotNull(dispatcher.Observe(new(118, true, false, 0, 10000), true).Press);
        store.Update("hotkeys", s => s.Hotkeys.Clear());
        Assert.Empty(configuration.ActiveBindings);
        dispatcher.Observe(new(118, false, false, 0, 10000), true);
        Assert.Null(dispatcher.Observe(new(118, true, false, 0, 10000), true).Press);
        settings.Dispose();
        store.Update("hotkeys", s => s.Hotkeys = new SettingsDocument().Hotkeys);
        Assert.Empty(configuration.ActiveBindings);
    }
}
