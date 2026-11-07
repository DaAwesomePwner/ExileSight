using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.Core.Input;

/// <summary>Uses the application's shared settings owner; does not open a second writer.</summary>
public sealed class HotkeySettings : IDisposable
{
    private readonly ISettingsStore<SettingsDocument> store;
    private readonly HotkeyConfiguration configuration;
    private readonly IFeatureCommandHandler[] handlers;
    private readonly object sync = new();
    private bool disposed;
    public HotkeySettings(ISettingsStore<SettingsDocument> store, HotkeyConfiguration configuration,
        IEnumerable<IFeatureCommandHandler> handlers)
    {
        this.store = store;
        this.configuration = configuration;
        this.handlers = handlers.ToArray();
        store.Changed += Changed;
        Reload();
    }
    public void Reload()
    {
        lock (sync)
        {
            if (disposed) return;
            var mapped = BindingMapper.Map(store.Snapshot.Hotkeys, configuration, handlers);
            configuration.Replace(mapped.Bindings, mapped.Rejected.Concat(store.Warnings));
        }
    }
    private void Changed(object? sender, SettingsChangedEventArgs e) => Reload();
    public void Dispose()
    {
        lock (sync) { disposed = true; store.Changed -= Changed; }
    }
}
