using Velopack;

namespace ExileSight.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // StartupObject selects this entry point; App.xaml remains an ApplicationDefinition.
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
