using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExileSight.Core.Settings;
using ExileSight.Features.MapInfo;
using ExileSight.App.Shell;
using ExileSight.App.Shell.Pages;
using ExileSight.App.Ui;
using ExileSight.Core.Input;
using ExileSight.Core.Overlay;
#endif
namespace ExileSight.App.Snapshots.Tests;

public sealed class SettingsPagesSnapshotTests
{
    [WindowsOnlyFact]
    public void BesideCursor() => RenderOverlay(false);
    [WindowsOnlyFact]
    public void Docked() => RenderOverlay(true);
    private static void RenderOverlay(bool docked)
    {
#if SNAPSHOT_WINDOWS
        var path = SnapshotRenderer.Render(() => ShellBuilder.Build(new("Overlay", "Standard", [], [], null), CardTheme.Default,
            settingsPage: OverlayPageBuilder.Build(new(new() { PlacementMode = docked ? CardPlacementMode.Docked : CardPlacementMode.BesideCursor }, new()), CardTheme.Default)),
            docked ? "settings-overlay-docked" : "settings-overlay-cursor", new Size(1280, 1100));
        Assert.True(File.Exists(path));
#endif
    }
    [WindowsOnlyFact]
    public void HostRejectsInvalidThresholdAndSavesValidEdits()
    {
#if SNAPSHOT_WINDOWS
        var directory = Path.Combine(Path.GetTempPath(), "settings-host-" + Guid.NewGuid());
        try
        {
            using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(directory), Path.Combine(directory, "settings.json"),
                SettingsJsonContext.Default.SettingsDocument, () => new());
            SnapshotRenderer.Render(() =>
            {
                var host = new OverlayPageHost(store, 16d / 9);
                T Find<T>(string name) where T : FrameworkElement => Descendants(host.Content).OfType<T>().Single(e => e.Name == name);
                var save = Descendants(host.Content).OfType<Button>().Single(b => Equals(b.Tag, "SaveOverlay"));
                Find<TextBox>("Quantity").Text = "-1";
                Assert.True(host.Dirty);
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, save));
                Assert.True(host.Dirty);
                Assert.Empty(store.Snapshot.Features.Sections);
                Find<TextBox>("Quantity").Text = "85";
                Find<CheckBox>("AutoScale").IsChecked = false;
                Find<TextBox>("Scale").Text = "1";
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, save));
                Assert.False(host.Dirty);
                Assert.Equal(1, store.Snapshot.Overlay.ScaleOverride);
                Assert.Equal(85, MapInfoSettings.Read(store.Snapshot.Features.Sections["mapinfo"]["settings"]).QuantityThreshold);
                return host;
            }, "settings-host-save", new Size(1000, 1000));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
#endif
    }
#if SNAPSHOT_WINDOWS
    private static IEnumerable<object> Descendants(object root)
    {
        yield return root;
        if (root is DependencyObject dependency)
            foreach (var child in LogicalTreeHelper.GetChildren(dependency))
                foreach (var nested in Descendants(child)) yield return nested;
    }
#endif
    [WindowsOnlyFact]
    public void TransparencyMixed()
    {
#if SNAPSHOT_WINDOWS
        var at = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var snapshot = new ActionLogSnapshot([
            new(at, "omni.press", "CopyItem", "NotForeground", "Game not in front", null),
            new(at.AddMinutes(-1), "hideout", "SendChatCommand", "Success", "", "/hideout"),
            new(at.AddMinutes(-2), "unnamed-id", "SetListingAmount", "Success", "", null)], 1, false);
        Assert.True(File.Exists(SnapshotRenderer.Render(() => ShellBuilder.Build(new("Transparency", "Standard", [], [], null), CardTheme.Default,
            settingsPage: TransparencyPageBuilder.Build(snapshot, CardTheme.Default)), "settings-transparency", new Size(1280, 820))));
#endif
    }
}
