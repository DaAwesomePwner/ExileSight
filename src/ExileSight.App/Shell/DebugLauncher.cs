using Microsoft.Extensions.DependencyInjection;
namespace ExileSight.App.Shell;

internal static class DebugLauncher
{
    private static DebugPanel? panel;
    internal static void Show(IServiceProvider services)
    {
        if (panel is null)
        {
            panel = new DebugPanel(services.GetServices<ExileSight.Core.Diagnostics.IDebugSection>());
            panel.Closed += (_, _) => panel = null;
        }
        panel.Show(); panel.WindowState = System.Windows.WindowState.Normal; panel.Activate();
    }
}
