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
        public IReadOnlyList<string> Warnings { get; set; } = [];
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
        configuration.UpdateGameFocused(true);
        var settings = new HotkeySettings(store, configuration, []);
        var dispatcher = new HotkeyDispatcher(TestIntents.Create(new FakeClock()), configuration);
        Assert.NotNull(dispatcher.Observe(new(118, true, false, 0, 10000), true).Press);
        store.Update("hotkeys", s => s.Hotkeys.Clear());
        Assert.Empty(configuration.ActiveBindings);
        dispatcher.Observe(new(118, false, false, 0, 10000), true);
        Assert.Null(dispatcher.Observe(new(118, true, false, 0, 10000), true).Press);
        settings.Dispose();
        store.Update("hotkeys", s => s.Hotkeys = new SettingsDocument().Hotkeys);
        Assert.Empty(configuration.ActiveBindings);
    }
    [Fact]
    public void ReloadPublishesDisabledReasonsAndStoreWarningsThenClearsRejections()
    {
        using var store = new Store();
        var configuration = new HotkeyConfiguration();
        configuration.UpdateGameFocused(true);
        using var settings = new HotkeySettings(store, configuration, []);
        var dispatcher = new HotkeyDispatcher(TestIntents.Create(new FakeClock()), configuration);
        store.Warnings = ["Binding 'copy-item' disabled by settings validation: invalid modifiers."];
        store.Update("hotkeys", s =>
        {
            s.Hotkeys[0].Enabled = false;
            s.Hotkeys[0].Modifiers = (Platform.Abstractions.Input.KeyModifiers)32;
            s.Hotkeys[1].Text = "/hideout\n/help";
        });
        Assert.Empty(configuration.ActiveBindings);
        Assert.Contains(configuration.RejectedBindings, r => r.Contains("copy-item: Disabled in settings: Invalid modifiers"));
        Assert.Contains(configuration.RejectedBindings, r => r.Contains("hideout: Chat text"));
        Assert.Contains(store.Warnings[0], configuration.RejectedBindings);
        Assert.Null(dispatcher.Observe(new(118, true, false, 0, 10000), true).Press);

        store.Warnings = [];
        store.Update("hotkeys", s => s.Hotkeys = new SettingsDocument().Hotkeys);
        Assert.Equal(2, configuration.ActiveBindings.Count);
        Assert.Empty(configuration.RejectedBindings);
        // Reload cannot turn a held key into a new physical press.
        Assert.Null(dispatcher.Observe(new(118, true, false, 0, 10000), true).Press);
        dispatcher.Observe(new(118, false, false, 0, 10000), true);
        Assert.NotNull(dispatcher.Observe(new(118, true, false, 0, 10000), true).Press);
    }
    [Fact]
    public void LateContextRegistrationRemapsUnknownBinding()
    {
        using var store = new Store();
        store.Snapshot.Hotkeys[0].Context = "late-overlay";
        var configuration = new HotkeyConfiguration();
        using var settings = new HotkeySettings(store, configuration, []);
        Assert.Contains(configuration.RejectedBindings, x => x.Contains("Unknown context"));
        configuration.Register("late-overlay");
        Assert.Empty(configuration.RejectedBindings);
        configuration.SetActive("late-overlay", true);
        Assert.Equal("copy-item", configuration.Match(118, 0)!.Id);
    }

    [Fact]
    public void OpeningSettingsFailurePublishesDefaultsAndDiagnostic()
    {
        var configuration = new HotkeyConfiguration();
        using var settings = new HotkeySettings(
            () => throw new IOException("Profile inaccessible"), configuration, []);
        Assert.Equal(2, configuration.ActiveBindings.Count);
        Assert.Contains("Settings unavailable; using default bindings: Profile inaccessible",
            configuration.RejectedBindings);
        configuration.Register("late-overlay");
        Assert.Contains(configuration.RejectedBindings, x => x.Contains("Profile inaccessible"));
    }
}
