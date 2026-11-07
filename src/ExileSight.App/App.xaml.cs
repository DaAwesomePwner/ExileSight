using System.Windows;
using ExileSight.App.Spikes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExileSight.App;

public partial class App : Application
{
    private IHost? host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<DebugPanel>();
        builder.Services.AddOverlaySpike().AddInputSpike();
        host = builder.Build();
        host.StartAsync().GetAwaiter().GetResult();
        MainWindow = host.Services.GetRequiredService<DebugPanel>();
        MainWindow.Show();
        OverlaySpike.StartOverlaySpike(host.Services);
        InputSpike.StartInputSpike(host.Services);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (host is not null)
        {
            host.StopAsync().GetAwaiter().GetResult();
            host.Dispose();
        }
        base.OnExit(e);
    }
}
