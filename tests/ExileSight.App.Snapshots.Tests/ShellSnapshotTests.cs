using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
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
        var output = SnapshotRenderer.Render(() => ShellBuilder.Build(model, CardTheme.Default, narrow),
            "shell-" + name, new Size(narrow ? 640 : 1280, narrow ? 1200 : 820));
        Assert.True(File.Exists(output));
#endif
    }
}
