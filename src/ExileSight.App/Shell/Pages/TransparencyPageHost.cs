using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.Core.Input;
namespace ExileSight.App.Shell.Pages;

public sealed class TransparencyPageHost : ContentControl
{
    public static string LogPath => ActionLogPath.Default;
    public TransparencyPageHost(ActionLogSnapshot snapshot, string path)
    {
        Content = TransparencyPageBuilder.Build(snapshot, CardTheme.Default);
        AddHandler(Button.ClickEvent, new RoutedEventHandler((_, args) =>
        {
            if (args.OriginalSource is not Button { Tag: "OpenActionsLog" }) return;
            args.Handled = true;
            try
            {
                var target = File.Exists(path) ? path : Path.GetDirectoryName(path)!;
                if (!File.Exists(target) && !Directory.Exists(target))
                { MessageBox.Show("No actions log has been created yet.", "ExileSight"); return; }
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
            { MessageBox.Show("Could not open actions.log: " + error.GetType().Name, "ExileSight"); }
        }));
    }
}
