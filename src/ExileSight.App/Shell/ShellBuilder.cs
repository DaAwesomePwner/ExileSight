using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using static ExileSight.App.Shell.ShellElements;
namespace ExileSight.App.Shell;

public static class ShellBuilder
{
    public static FrameworkElement Build(ShellModel model, CardTheme t, bool narrow = false)
    {
        var root = new DockPanel { Background = t.AppBackground, LastChildFill = true };
        var nav = new WrapPanel { Orientation = narrow ? Orientation.Horizontal : Orientation.Vertical,
            Background = t.AppNav, Width = narrow ? double.NaN : 200, Margin = new Thickness(0, 0, 16, 0) };
        nav.Children.Add(Text("  ◉  ExileSight", t, 18, true));
        foreach (var name in new[] { "Home", "Item rules", "Map log", "Sales", "Overlay", "Hotkeys", "Transparency" })
        {
            var button = Button(name, name, t); button.HorizontalContentAlignment = HorizontalAlignment.Left;
            if (!narrow) button.Width = 184;
            if (model.Page == name) button.Background = t.RollTrack;
            nav.Children.Add(button);
        }
        DockPanel.SetDock(nav, narrow ? Dock.Top : Dock.Left); root.Children.Add(nav);
        var content = new DockPanel { Margin = new Thickness(16, 24, 20, 16) };
        var footer = new StackPanel();
        footer.Children.Add(Text("This product isn't affiliated with or endorsed by Grinding Gear Games in any way.", t, 12, muted: true));
        footer.Children.Add(Button("See everything it sent · Transparency", "Transparency", t));
        DockPanel.SetDock(footer, Dock.Bottom); content.Children.Add(footer);
        FrameworkElement page = model.Page switch
        {
            "Home" => HomeBuilder.Build(model, t, narrow),
            "Hotkeys" => HotkeysBuilder.Build(model, t),
            _ => PlaceholderBuilder.Build(model.Page, t)
        };
        content.Children.Add(new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        root.Children.Add(content); return root;
    }
}
public static class HotkeysBuilder
{
    public static FrameworkElement Build(ShellModel model, CardTheme t)
    {
        var panel = new StackPanel(); panel.Children.Add(Text("Hotkeys", t, 24, true));
        panel.Children.Add(Text("Read-only. Bindings are set in the active profile JSON's hotkeys section in the settings folder. Close ExileSight before editing, then restart.", t, muted: true));
        panel.Children.Add(Card(Keys(model.Keys, t), t)); return panel;
    }
}
public static class PlaceholderBuilder
{
    public static FrameworkElement Build(string page, CardTheme t)
    {
        var panel = new StackPanel(); panel.Children.Add(Text(page, t, 24, true));
        panel.Children.Add(Text("This page is being prepared. Existing settings remain in your profile.", t, muted: true));
        return panel;
    }
}
