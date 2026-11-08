using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ExileSight.App.Shell;
using ExileSight.App.Ui;
using ExileSight.Core.Status;
#endif
namespace ExileSight.App.Snapshots.Tests;

public sealed class ShellSnapshotTests
{
    [WindowsOnlyFact]
    public void HomeReady() => Render("home-ready", false, false, "Home");
    [WindowsOnlyFact]
    public void HomeProblemAndMissingSession() => Render("home-problem", true, false, "Home");
    [WindowsOnlyFact]
    public void Hotkeys() => Render("hotkeys", false, false, "Hotkeys");
    [WindowsOnlyFact]
    public void NarrowLayout() => Render("home-narrow", false, true, "Home");
    [WindowsOnlyFact]
    public void DarkControlTemplates()
    {
#if SNAPSHOT_WINDOWS
        SnapshotRenderer.Render(() =>
        {
            var theme = CardTheme.Default;
            var model = new ShellModel("Home", "Standard", [], [], null);
            var root = new StackPanel { Background = theme.AppBackground, Margin = new Thickness(16),
                Resources = ShellBuilder.Build(model, theme).Resources };
            var combo = new ComboBox { IsEditable = true, Text = "Standard", Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
            combo.Items.Add("Standard"); combo.Items.Add("Hardcore"); root.Children.Add(combo);
            // The same item templates used in the popup, rendered in a detached preview.
            var items = new StackPanel();
            items.Children.Add(new ComboBoxItem { Content = "Standard", IsSelected = true });
            items.Children.Add(new ComboBoxItem { Content = "Hardcore" });
            root.Children.Add(new Border { Background = theme.AppPanel, BorderBrush = theme.OverlayBorder,
                BorderThickness = new Thickness(1), Child = items, Width = 240, HorizontalAlignment = HorizontalAlignment.Left });
            var scroll = new ScrollViewer { Width = 280, Height = 120, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
                Content = new Border { Width = 560, Height = 240, Background = theme.AppPanel } };
            root.Children.Add(scroll);
            root.Measure(new Size(400, 320)); root.Arrange(new Rect(0, 0, 400, 320)); root.UpdateLayout();
            Assert.NotNull(combo.Template.FindName("PART_EditableTextBox", combo));
            Assert.IsType<Popup>(combo.Template.FindName("PART_Popup", combo));
            Assert.Same(theme.AppPanel, combo.Background);
            Assert.NotNull(scroll.Template.FindName("PART_VerticalScrollBar", scroll));
            Assert.NotNull(scroll.Template.FindName("PART_HorizontalScrollBar", scroll));
            return root;
        }, "shell-dark-controls", new Size(400, 320));
#endif
    }
    private static void Render(string name, bool problem, bool narrow, string page)
    {
#if SNAPSHOT_WINDOWS
        var at = new DateTimeOffset(2025, 3, 5, 10, 0, 0, TimeSpan.Zero);
        var session = LastSession.Map([new(at, at.AddMinutes(6), 360, 0),
            new(at.AddMinutes(8), at.AddMinutes(15), 420, 1), new(at.AddMinutes(18), at.AddMinutes(23), 300, 0)],
            [new(at.AddMinutes(12), 7, "divine")]);
        var model = new ShellModel(page, "Standard",
            [HomeStatus.Game(!problem, false, true), HomeStatus.Log("Following new entries"),
             HomeStatus.Prices(true, false, "Saved Currency prices"), HomeStatus.Packs("Packs ready")],
            [new("CapsLock", TriggerNames.Display("omni.press"), true), new("F5", TriggerNames.Display("macros.travel"), true),
             new("Ctrl + Shift + M", TriggerNames.Display("maptracker.viewer"), true),
             new("Tab", TriggerNames.Display("timers.hold"), false)], problem ? null : session);
        Assert.True(model.SameContent(model with { Status = model.Status.ToArray(), Keys = model.Keys.ToArray(),
            Session = model.Session is { } current ? current with { Maps = current.Maps.ToArray() } : null }));
        Assert.False(model.SameContent(model with { League = "Other league" }));
        var output = SnapshotRenderer.Render(() => ShellBuilder.Build(model, CardTheme.Default, narrow),
            "shell-" + name, new Size(narrow ? 640 : 1280, narrow ? 1200 : 820));
        Assert.True(File.Exists(output));
#endif
    }
}
