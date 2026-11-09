using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.Features.Guide;
using static ExileSight.App.Shell.ShellElements;

namespace ExileSight.App.Features.Guide;

public enum GuidePageAction { Back, Next, Import, Export, RemoveCustom, Reset, LeagueStart, Bandit }
public sealed record GuidePageModel(GuideSnapshot? Snapshot, string? Status = null);

/// <summary>Pure desktop presentation; buttons carry local action IDs, never game commands.</summary>
public static class GuidePageBuilder
{
    public static FrameworkElement Build(GuidePageModel model, CardTheme theme)
    {
        var page = new StackPanel();
        page.Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/ExileSight.App;component/Shell/ShellControls.xaml", UriKind.Relative) });
        foreach (var key in new[] { "AppPanel", "AppNav", "OverlayBorder", "TextSecondary", "Accent" }) page.Resources[key] = theme[key];
        page.Children.Add(Text("Act guide", theme, 24, true));
        var state = model.Snapshot;
        page.Children.Add(Text(state?.ProfileNotice ?? "Guide unavailable.", theme, muted: true));
        page.Children.Add(Text("Timers pause after two minutes without zone, level-up or manual activity. Restart starts paused.", theme, muted: true));
        var controls = new StackPanel();
        controls.Children.Add(Text("Run controls", theme, 18, true));
        var navigation = new WrapPanel();
        Add(navigation, "Back", GuidePageAction.Back); Add(navigation, "Next", GuidePageAction.Next);
        Add(navigation, "Reset run", GuidePageAction.Reset);
        controls.Children.Add(navigation);
        var settings = new WrapPanel();
        Add(settings, $"League start: {(state?.LeagueStart == true ? "yes" : "no")} (resets)", GuidePageAction.LeagueStart);
        Add(settings, $"Bandit: {state?.Bandit ?? "none"} (resets)", GuidePageAction.Bandit);
        controls.Children.Add(settings);
        var files = new WrapPanel();
        Add(files, "Import JSON (resets)", GuidePageAction.Import);
        Add(files, "Export CSV", GuidePageAction.Export);
        Add(files, "Remove custom guide", GuidePageAction.RemoveCustom);
        controls.Children.Add(files);
        if (state?.Notice is { } notice) controls.Children.Add(Text(notice, theme, muted: true));
        if (model.Status is { } status) controls.Children.Add(Text(status, theme, muted: true));
        page.Children.Add(Card(controls, theme));
        if (state?.Act is { } act)
        {
            var current = new StackPanel();
            current.Children.Add(Text($"Act {act} · step {state.Step} of {state.StepCount}", theme, 18, true));
            foreach (var line in state.Lines) current.Children.Add(Text(line, theme));
            if (state.NextLine is { } next) current.Children.Add(Text("NEXT  " + next, theme, muted: true));
            page.Children.Add(Card(current, theme));
        }
        return page;

        void Add(Panel parent, string label, GuidePageAction action)
        {
            var button = Button(label, "", theme); button.Tag = action;
            button.IsEnabled = state is not null;
            parent.Children.Add(button);
        }
    }
}
