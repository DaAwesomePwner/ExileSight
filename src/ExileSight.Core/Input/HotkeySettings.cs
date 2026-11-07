using ExileSight.Core.Settings;
using ExileSight.Core.Features;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.Core.Input;

/// <summary>Uses the application's shared settings owner; never opens a second writer.
/// Startup/read failures publish defaults and diagnostics instead of aborting the app.</summary>
public sealed class HotkeySettings : IDisposable
{
    private readonly ISettingsStore<SettingsDocument>? store;
    private readonly HotkeyConfiguration configuration;
    private readonly IFeatureCommandHandler[] handlers;
    private readonly FeatureDefaultBinding[] defaults;
    private readonly object sync = new();
    private readonly string? startupWarning;
    private bool disposed;
    public HotkeySettings(ISettingsStore<SettingsDocument> store, HotkeyConfiguration configuration,
        IEnumerable<IFeatureCommandHandler> handlers) : this(() => store, configuration, handlers) { }
    public HotkeySettings(Func<ISettingsStore<SettingsDocument>> open, HotkeyConfiguration configuration,
        IEnumerable<IFeatureCommandHandler> handlers, IEnumerable<FeatureDefaultBinding>? defaults = null)
    {
        this.configuration = configuration;
        this.handlers = handlers.ToArray();
        this.defaults = (defaults ?? []).ToArray();
        try { store = open(); }
        catch (Exception error) { startupWarning = $"Settings unavailable; using default bindings: {error.Message}"; }
        if (store is not null) store.Changed += Changed;
        configuration.ContextRegistered += Reload;
        Reload();
    }
    public void Reload()
    {
        lock (sync)
        {
            if (disposed) return;
            SettingsDocument document;
            IReadOnlyList<string> warnings;
            try
            {
                document = store?.Snapshot ?? new();
                warnings = store?.Warnings ?? (startupWarning is null ? [] : [startupWarning]);
            }
            catch (Exception error)
            {
                document = new();
                warnings = [$"Settings unavailable; using default bindings: {error.Message}"];
            }
            var mapped = BindingMapper.Map(FeatureDefaultBinding.Apply(document.Hotkeys, defaults), configuration, handlers);
            configuration.Replace(mapped.Bindings, mapped.Rejected.Concat(warnings));
        }
    }
    private void Changed(object? sender, SettingsChangedEventArgs e) => Reload();
    public void Dispose()
    {
        lock (sync)
        {
            disposed = true;
            if (store is not null) store.Changed -= Changed;
            configuration.ContextRegistered -= Reload;
        }
    }
}
