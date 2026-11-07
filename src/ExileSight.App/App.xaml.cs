using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ExileSight.App.FirstRun;
using ExileSight.App.Input;
using ExileSight.App.Overlay;
using ExileSight.App.Tray;
using ExileSight.App.Updates;
using ExileSight.Core.Diagnostics;
using ExileSight.Core.Games;
using ExileSight.Core.Settings;
using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Windows.Install;
using ExileSight.Platform.Windows.Tray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExileSight.App;

public partial class App : Application
{
    private IHost? host;
    private bool stopping;
    private bool setupOpen;
    private readonly AppPaths paths = new();
    private ProfileStore? profiles;
    private ITransactionalSettingsStore<SettingsDocument>? settings;
    private readonly TrayDiagnostics trayDiagnostics = new();
    private Task? shutdownTask;
    private readonly ExitPolicy exitPolicy = new();
    private SettingsDiagnostics? settingsDiagnostics;
    private readonly LocalPathResolver localPaths = new();
    private ClientLogSession? logs;
    private TrayHome? tray;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            profiles = new ProfileStore(paths, Game.PoE1);
            settings = profiles.Open(profiles.ActiveProfileId);
            logs = new ClientLogSession(settings, pathResolver: localPaths);
            settingsDiagnostics = new(profiles, settings, paths);
            var builder = Host.CreateApplicationBuilder();
            // App owns one active profile for this process; switching is not exposed yet.
            builder.Services.AddSingleton<IAppPaths>(paths);
            builder.Services.AddSingleton(profiles);
            builder.Services.AddSingleton<IProfileStore>(profiles);
            builder.Services.AddSingleton<ISettingsStore<SettingsDocument>>(settings);
            builder.Services.AddSingleton(exitPolicy);
            builder.Services.AddSingleton<IDebugSection>(settingsDiagnostics);
            builder.Services.AddSingleton<DebugPanel>();
            builder.Services.AddSingleton<IDebugSection>(trayDiagnostics);
            builder.Services.AddUpdates();
            builder.Services.AddSingleton<IDebugSection>(logs);
            builder.Services.AddOverlays().AddProductionInput();
            host = builder.Build();
            if (!ReferenceEquals(profiles, host.Services.GetRequiredService<ProfileStore>()) ||
                !ReferenceEquals(profiles, host.Services.GetRequiredService<IProfileStore>()) ||
                !ReferenceEquals(settings, host.Services.GetRequiredService<ISettingsStore<SettingsDocument>>()) ||
                !ReferenceEquals(paths, host.Services.GetRequiredService<IAppPaths>()))
                throw new InvalidOperationException("Production consumers must share App settings owners.");
            await Task.Run(() => host.StartAsync());
            MainWindow = host.Services.GetRequiredService<DebugPanel>();
            MainWindow.Closing += CloseAsync;
            tray = new TrayHome(new ShellTrayIconFactory(), trayDiagnostics, () => { MainWindow.Show(); MainWindow.Activate(); },
                () => ShowSetup(), OpenSettingsFolder, async () => await StopAndShutdownAsync());
            var general = settings.Snapshot.General;
            if (!general.FirstRunCompleted || await Task.Run(() => ClientLogDiscovery.Validate(general.ClientTxtPath, localPaths)) is not null)
            {
                if (!ShowSetup() && !general.FirstRunCompleted) { await StopAndShutdownAsync(); return; }
            }
            settings.Changed += SettingsChanged;
            await logs.RefreshAsync();
            host.Services.StartOverlays();
            settingsDiagnostics.OverlayAttached = true;
            InputRegistration.StartProductionInput(host.Services);
            settingsDiagnostics.InputAttached = true;
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "ExileSight startup failed");
            await StopAndShutdownAsync();
        }
    }

    private void CloseAsync(object? sender, CancelEventArgs e)
    {
        if (stopping) return;
        e.Cancel = true;
        MainWindow.Hide();
    }

    private bool ShowSetup()
    {
        if (setupOpen || stopping) return false;
        setupOpen = true;
        try { return new SetupWizard(settings!, new InstallLocations()).ShowDialog() == true; }
        finally { setupOpen = false; }
    }

    private async void SettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (logs is not null) await logs.RefreshAsync();
    }

    private void OpenSettingsFolder()
    {
        try
        {
            var folder = paths.GetSettingsDirectory("poe1");
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception error) { MessageBox.Show(error.Message, "Cannot open settings folder"); }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        if (e.Cancel) return;
        exitPolicy.BeginSessionEnding();
        var completion = StopAndShutdownAsync();
        if (!completion.IsCompleted)
        {
            // WPF's synchronous session-ending event must wait for async cleanup while
            // still servicing dispatcher work needed by hosted UI services.
            var frame = new DispatcherFrame();
            completion.GetAwaiter().OnCompleted(() => frame.Continue = false);
            var deadline = new DispatcherTimer { Interval = exitPolicy.StopTimeout };
            deadline.Tick += (_, _) => frame.Continue = false;
            deadline.Start();
            Dispatcher.PushFrame(frame);
            deadline.Stop();
        }
    }

    private Task StopAndShutdownAsync() => shutdownTask ??= StopCoreAsync();

    private async Task StopCoreAsync()
    {
        stopping = true;
        try { tray?.Dispose(); }
        catch (Exception error) { trayDiagnostics.Record(error); }
        if (settings is not null) settings.Changed -= SettingsChanged;
        // Flush first and off the dispatcher so session-ending's deadline remains responsive.
        await Task.Run(() =>
        {
            try { settings?.Flush(); }
            catch (Exception error) { trayDiagnostics.Record(error); }
            finally
            {
                try { profiles?.Dispose(); }
                catch (Exception error) { trayDiagnostics.Record(error); }
            }
        });
        try { if (logs is not null) await logs.DisposeAsync(); }
        catch (Exception error) { trayDiagnostics.Record(error); }
        try
        {
            if (host is not null)
                await Task.Run(() => host.StopAsync(exitPolicy.StopTimeout));
        }
        catch (Exception error) { trayDiagnostics.Record(error); }
        finally
        {
            try { settings?.Dispose(); }
            catch (Exception error) { trayDiagnostics.Record(error); }
            try { host?.Dispose(); }
            catch (Exception error) { trayDiagnostics.Record(error); }
            if (MainWindow is not null) MainWindow.Closing -= CloseAsync;
            if (!exitPolicy.IsSessionEnding) Shutdown();
        }
    }
}
