using System.Windows;
using System.Windows.Controls;
using ExileSight.Features.Macros;

namespace ExileSight.App.Features.Macros;

public sealed class MacroSettingsWindow : Window
{
    public MacroSettingsWindow(MacroWheel model, string title)
    {
        Title = title;
        Width = 660; Height = 620;
        var panel = new StackPanel { Margin = new Thickness(16) };
        Content = new ScrollViewer { Content = panel };
        panel.Children.Add(new TextBlock { Text = "Short travel press: /hideout. Short macro press: command 0. Hold then release for wheel.\n" +
            "Blank slots are disabled. Each command must be one line, at most 400 characters.\n" +
            "Destinations must start with /. Commands may contain your own chat text.", TextWrapping = TextWrapping.Wrap });
        var settings = model.ReadSettings();
        panel.Children.Add(new TextBlock { Text = "Command 0 (short macro press, separate from wheel)" });
        var command0 = new TextBox { Text = settings.Command0, Margin = new Thickness(2) };
        panel.Children.Add(command0);
        var commands = new TextBox[8];
        var destinations = new TextBox[8];
        for (var i = 0; i < 8; i++)
        {
            panel.Children.Add(new TextBlock { Text = $"Slot {i}: command / destination" });
            var row = new Grid();
            row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new());
            commands[i] = new TextBox { Text = settings.Commands[i], Margin = new Thickness(2) };
            destinations[i] = new TextBox { Text = settings.Destinations[i], Margin = new Thickness(2) };
            Grid.SetColumn(destinations[i], 1);
            row.Children.Add(commands[i]); row.Children.Add(destinations[i]); panel.Children.Add(row);
        }
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var save = new Button { Content = "Save", Margin = new Thickness(4) };
        panel.Children.Add(status); panel.Children.Add(save);
        save.Click += (_, _) =>
        {
            var value = new MacroSettings(commands.Select(t => t.Text).ToArray(), destinations.Select(t => t.Text).ToArray(), command0.Text);
            if (!value.IsValid) { status.Text = "Check lengths, line breaks and destination slash commands."; return; }
            try { model.SaveSettings!(value); Close(); }
            catch (Exception) { status.Text = "Settings could not be saved."; }
        };
    }
}
