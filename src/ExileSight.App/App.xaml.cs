using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ExileSight.App.FirstRun;
using ExileSight.App.Spikes;
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
    private bool sessionEnding;
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
            logs = new ClientLogSession(settings);
            var builder = Host.CreateApplicationBuilder();
            // App owns one active profile for this process; switching is not exposed yet.
            builder.Services.AddSingleton<ISettingsStore<SettingsDocument>>(settings);
            builder.Services.AddSingleton<DebugPanel>();
            builder.Services.AddSingleton<IDebugSection>(trayDiagnostics);
            builder.Services.AddUpdates();
            builder.Services.AddSingleton<IDebugSection>(logs);
            builder.Services.AddOverlaySpike().AddInputSpike();
            host = builder.Build();
            await Task.Run(() => host.StartAsync());
            MainWindow = host.Services.GetRequiredService<DebugPanel>();
            MainWindow.Closing += CloseAsync;
            tray = new TrayHome(new ShellTrayIconFactory(), trayDiagnostics, () => { MainWindow.Show(); MainWindow.Activate(); },
                () => ShowSetup(), OpenSettingsFolder, async () => await StopAndShutdownAsync());
            var general = settings.Snapshot.General;
            if (!general.FirstRunCompleted || ClientLogDiscovery.Validate(general.ClientTxtPath) is not null)
            {
                if (!ShowSetup() && !general.FirstRunCompleted) { await StopAndShutdownAsync(); return; }
            }
            settings.Changed += SettingsChanged;
            await logs.RefreshAsync();
            OverlaySpike.StartOverlaySpike(host.Services);
            InputSpike.StartInputSpike(host.Services);
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
        sessionEnding = true;
        var completion = StopAndShutdownAsync();
        if (!completion.IsCompleted)
        {
            // WPF's synchronous session-ending event must wait for async cleanup while
            // still servicing dispatcher work needed by hosted UI services.
            var frame = new DispatcherFrame();
            completion.GetAwaiter().OnCompleted(() => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
    }

    private Task StopAndShutdownAsync() => shutdownTask ??= StopCoreAsync();

    private async Task StopCoreAsync()
    {
        stopping = true;
        try { tray?.Dispose(); }
        catch (Exception error) { trayDiagnostics.Record(error); }
        if (settings is not null) settings.Changed -= SettingsChanged;
        try
        {
            if (logs is not null) await logs.DisposeAsync();
            settings?.Flush();
            profiles?.Dispose();
        }
        catch (Exception error) { trayDiagnostics.Record(error); }
        try
        {
            if (host is not null)
                await Task.Run(() => host.StopAsync(TimeSpan.FromSeconds(5)));
        }
        catch (Exception error) { trayDiagnostics.Record(error); }
        finally
        {
            try { settings?.Dispose(); }
            catch (Exception error) { trayDiagnostics.Record(error); }
            try { host?.Dispose(); }
            catch (Exception error) { trayDiagnostics.Record(error); }
            if (MainWindow is not null) MainWindow.Closing -= CloseAsync;
            if (!sessionEnding) Shutdown();
        }
    }
}
