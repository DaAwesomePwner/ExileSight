using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExileSight.Core.Settings;
using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Install;
using ExileSight.Platform.Abstractions.Storage;
using Microsoft.Win32;

namespace ExileSight.App.FirstRun;

public sealed class SetupWizard : Window
{
    private readonly ITransactionalSettingsStore<SettingsDocument> settings;
    private readonly StackPanel body = new() { Margin = new Thickness(24) };
    private readonly ComboBox path = new() { IsEditable = true, MinWidth = 470 };
    private readonly TextBlock message = new() { TextWrapping = TextWrapping.Wrap };
    private int page;
    private readonly ILocalPathResolver resolver = new ExileSight.Platform.Windows.Install.LocalPathResolver();

    public SetupWizard(ITransactionalSettingsStore<SettingsDocument> settings, IInstallLocations locations)
    {
        this.settings = settings;
        Title = "Welcome to ExileSight";
        Width = 640; Height = 500;
        Content = new ScrollViewer { Content = body };
        path.ItemsSource = ClientLogDiscovery.Candidates(locations, resolver).Where(p => ClientLogDiscovery.Validate(p, resolver) is null).ToArray();
        path.Text = settings.Snapshot.General.ClientTxtPath ?? path.Items.Cast<string>().FirstOrDefault() ?? "";
        Render();
    }

    private void Text(string value) => body.Children.Add(new TextBlock
        { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
    private void Button(string label, Action action)
    {
        var button = new System.Windows.Controls.Button { Content = label, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(8) };
        button.Click += (_, _) => action();
        body.Children.Add(button);
    }

    private void Render()
    {
        body.Children.Clear();
        Text($"Setup — {page + 1} of 4");
        switch (page)
        {
            case 0:
                Text("ExileSight reads Client.txt read-only to show area changes and level-ups. It never changes game files. Clipboard text is read only after your own hotkey. Each key press can send one game action.");
                Text("Current hotkeys:\n" + string.Join("\n", settings.Snapshot.Hotkeys.Select(b =>
                    $"{b.Id}: {b.Modifiers} + {System.Windows.Input.KeyInterop.KeyFromVirtualKey(b.Key)} ({(b.Enabled ? "enabled" : "disabled")})")));
                Text("Network hosts: GitHub and poe.ninja only. Nothing from pathofexile.com.\nThis product isn't affiliated with or endorsed by Grinding Gear Games in any way.");
                break;
            case 1:
                Text("Choose your game's log. Detected readable candidates are listed below; Browse can locate another install.");
                body.Children.Add(path);
                Button("Browse…", () =>
                {
                    var picker = new OpenFileDialog { Filter = "Client logs|Client.txt;KakaoClient.txt;LatestClient.txt", CheckFileExists = true };
                    if (picker.ShowDialog(this) == true) path.Text = picker.FileName;
                    message.Text = ClientLogDiscovery.Validate(path.Text, resolver) ?? "Log is readable.";
                });
                break;
            case 2:
                Text("Use Windowed or Windowed Fullscreen. Overlays cannot draw over exclusive Fullscreen. Change this yourself in the game's display settings. ExileSight does not read or change production_Config.ini.");
                break;
            case 3:
                Text("Ready. Finish saves the log path and starts ExileSight in the notification area. Use the tray icon to open diagnostics, run setup again, or exit.\n\n" + path.Text);
                break;
        }
        message.Text = "";
        body.Children.Add(message);
        if (page > 0) Button("Back", () => { page--; Render(); });
        Button(page == 3 ? "Finish" : "Next", Next);
        Button("Cancel", () => { DialogResult = false; });
    }

    private void Next()
    {
        if (page is 1 or 3 && ClientLogDiscovery.Validate(path.Text, resolver) is { } error)
        { message.Text = error; return; }
        if (page < 3) { page++; Render(); return; }
        try
        {
            SetupCommit.Finish(settings, path.Text, resolver);
            DialogResult = true;
        }
        catch (Exception saveError) when (saveError is IOException or UnauthorizedAccessException or InvalidOperationException)
        { message.Text = "Could not save setup: " + saveError.Message; }
    }
}
