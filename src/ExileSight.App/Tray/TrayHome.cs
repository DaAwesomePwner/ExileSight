using Forms = System.Windows.Forms;

namespace ExileSight.App.Tray;

public sealed class TrayHome : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu = new();
    public TrayHome(Action debug, Action setup, Action folder, Action exit)
    {
        menu.Items.Add("Debug panel", null, (_, _) => debug());
        menu.Items.Add("Run setup again", null, (_, _) => setup());
        menu.Items.Add("Open settings folder", null, (_, _) => folder());
        menu.Items.Add("Exit", null, (_, _) => exit());
        icon = new() { Text = "ExileSight", Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => debug();
    }
    public void Dispose() { icon.Visible = false; icon.Dispose(); menu.Dispose(); }
}
