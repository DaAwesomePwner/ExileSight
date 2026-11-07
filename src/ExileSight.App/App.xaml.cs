using System.ComponentModel;
using System.Windows;
using ExileSight.App.Spikes;
using ExileSight.App.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExileSight.App;

public partial class App : Application
{
    private IHost? host;
    private bool stopping;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton<DebugPanel>();
            builder.Services.AddUpdates();
            builder.Services.AddOverlaySpike().AddInputSpike();
            host = builder.Build();
            await Task.Run(() => host.StartAsync());
            MainWindow = host.Services.GetRequiredService<DebugPanel>();
            MainWindow.Closing += CloseAsync;
            MainWindow.Show();
            OverlaySpike.StartOverlaySpike(host.Services);
            InputSpike.StartInputSpike(host.Services);
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "ExileSight startup failed");
            await StopAndShutdownAsync();
        }
    }

    private async void CloseAsync(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        if (stopping)
            return;
        stopping = true;
        await StopAndShutdownAsync();
    }

    private async Task StopAndShutdownAsync()
    {
        try
        {
            if (host is not null)
                await Task.Run(() => host.StopAsync());
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "ExileSight shutdown failed");
        }
        finally
        {
            host?.Dispose();
            if (MainWindow is not null)
                MainWindow.Closing -= CloseAsync;
            Shutdown();
        }
    }
}
