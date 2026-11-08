#if SNAPSHOT_WINDOWS
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Shell;
using ExileSight.App.Ui;
using Xunit;
namespace ExileSight.App.Snapshots.Tests;

internal static class SettingsPageTestTheme
{
    internal static T Apply<T>(T host) where T : FrameworkElement
    {
        var theme = CardTheme.Default;
        // Reuse the real shell's resource dictionary, including its theme token bindings.
        host.Resources.MergedDictionaries.Add(ShellBuilder.Build(new("Home", "Standard", [], [], null), theme).Resources);
        if (host is Panel panel) panel.Background = theme.AppBackground;
        if (host is Control control) control.Background = theme.AppBackground;
        foreach (var type in new[] { typeof(RadioButton), typeof(CheckBox), typeof(ComboBox) })
            Assert.IsType<Style>(host.TryFindResource(type));
        return host;
    }
}
#endif
