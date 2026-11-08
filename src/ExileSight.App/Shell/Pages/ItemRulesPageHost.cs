using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ExileSight.App.Ui;
using ExileSight.Core.Settings;
using ExileSight.Features.ItemInfo;
using ExileSight.Features.Omni;
using ExileSight.Platform.Abstractions.Storage;
namespace ExileSight.App.Shell.Pages;

public sealed class ItemRulesPageHost : ContentControl
{
    private readonly ISettingsStore<SettingsDocument> store;
    private readonly ItemInfoViewModel? source;
    private readonly string[] translations;
    private ItemInfoSettings settings = ItemInfoSettings.Default;
    private OmniSettings omni = OmniSettings.Default;
    private bool building;
    public bool Dirty { get; private set; }
    public ItemRulesPageHost(ISettingsStore<SettingsDocument> store, ItemInfoViewModel? source, string[] translations)
    {
        this.store = store; this.source = source; this.translations = translations;
        Reload();
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(Changed));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(Changed));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(Changed));
        AddHandler(Button.ClickEvent, new RoutedEventHandler(Clicked));
        Loaded += (_, _) => { if (source is not null) source.PropertyChanged += SourceChanged; UpdatePreview(); };
        Unloaded += (_, _) => { if (source is not null) source.PropertyChanged -= SourceChanged; };
    }
    private T Find<T>(string name) where T : FrameworkElement =>
        ShellViewState.Find<T>(Content as DependencyObject).First(e => e.Name == name);
    private void SourceChanged(object? sender, PropertyChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(UpdatePreview));
    private void Reload()
    {
        var document = store.Snapshot;
        settings = ItemInfoSettings.Load(new OverlaySettingsContext(document, "iteminfo"));
        omni = OmniSettings.Load(new OverlaySettingsContext(document, "omni"));
        Rebuild(); Dirty = false;
    }
    private void Rebuild()
    {
        building = true;
        Content = ItemRulesPageBuilder.Build(new(settings, omni), CardTheme.Default);
        building = false; UpdatePreview();
    }
    private void Capture()
    {
        if (settings.Profiles.Length > 0)
        {
            var rules = Find<StackPanel>("RuleRows").Children.OfType<WrapPanel>().SelectMany(panel =>
            {
                var row = (ItemRulesRow)panel.Tag;
                var classes = panel.Children.OfType<TextBox>().Single().Text.Split(',');
                var match = panel.Children.OfType<RadioButton>().FirstOrDefault(b => b.IsChecked == true)?.Tag;
                return RuleText.Create(row.Text, match is Highlight h ? h : row.Match, classes, row.Implicit, row.Enchant);
            }).ToArray();
            var profiles = settings.Profiles.ToArray();
            profiles[settings.ActiveProfile] = profiles[settings.ActiveProfile] with { Rules = rules };
            settings = settings with { Profiles = profiles };
        }
        bool On(string name) => Find<CheckBox>(name).IsChecked == true;
        settings = settings with { ShowCompare = On("Compare"), ShowDefenceBasePercentile = On("Defence"),
            ShowItemLevelColumn = On("Level"), ShowImplicits = On("Implicits") };
        omni = new(On("Advanced"));
    }
    private void Changed(object sender, RoutedEventArgs e)
    {
        if (building) return;
        if (e.OriginalSource is TextBox { Name: "RuleSearch" } search)
        {
            var query = search.Text.Trim();
            Find<ListBox>("RuleResults").ItemsSource = query.Length < 2 ? [] : translations
                .Where(text => text.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(100).ToArray();
            return;
        }
        Capture(); Dirty = true; UpdatePreview();
    }
    private void UpdatePreview()
    {
        var result = source?.LastCopied;
        var card = result is null ? null : ItemCardBuilder.Build(result with
        {
            Settings = settings,
            Mods = result.Mods.Select(m => m with { Highlight = settings.Evaluate(result.Item, m.Source) }).ToArray()
        }, settings: settings, comparison: source?.LastComparison);
        Find<ContentControl>("ItemPreview").Content = ItemRulesPageBuilder.Preview(card, CardTheme.Default);
    }
    private void Clicked(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button { Tag: string action } ||
            !(action is "SaveRules" or "DiscardRules" or "AddRule" || action.StartsWith("Profile", StringComparison.Ordinal) ||
              action.StartsWith("Remove", StringComparison.Ordinal))) return;
        e.Handled = true;
        if (action == "DiscardRules") { Reload(); return; }
        Capture();
        if (action == "SaveRules")
        {
            try
            {
                settings.Validate();
                store.Update("iteminfo", document =>
                {
                    settings.Save(new OverlaySettingsContext(document, "iteminfo"));
                    omni.Save(new OverlaySettingsContext(document, "omni"));
                });
                Dirty = false; Find<TextBlock>("RulesNotice").Text = "Changes saved.";
            }
            catch (Exception error) when (error is ArgumentException or System.IO.IOException or InvalidOperationException)
            { Find<TextBlock>("RulesNotice").Text = "Not saved. " + error.GetType().Name; }
            return;
        }
        if (action.StartsWith("Profile", StringComparison.Ordinal))
        {
            var index = int.Parse(action[7..], System.Globalization.CultureInfo.InvariantCulture);
            var profiles = settings.Profiles.ToList();
            while (profiles.Count <= index) profiles.Add(new($"Profile {profiles.Count + 1}", []));
            settings = settings with { Profiles = profiles.ToArray(), ActiveProfile = index };
        }
        else
        {
            var profiles = settings.Profiles.Length == 0 ? [new HighlightProfile("Profile 1", [])] : settings.Profiles.ToArray();
            var profile = profiles[settings.ActiveProfile];
            if (action == "AddRule")
            {
                if (Find<ListBox>("RuleResults").SelectedItem is not string text) return;
                profile = profile with { Rules = profile.Rules.Concat(RuleText.Create(text, Highlight.Desired)).Distinct().ToArray() };
            }
            else
            {
                var index = int.Parse(action[6..], System.Globalization.CultureInfo.InvariantCulture);
                profile = profile with { Rules = ItemRulesRow.From(profile).Where((_, i) => i != index)
                    .SelectMany(r => RuleText.Create(r.Text, r.Match, r.Classes.Split(','), r.Implicit, r.Enchant)).ToArray() };
            }
            profiles[settings.ActiveProfile] = profile;
            settings = settings with { Profiles = profiles };
        }
        Dirty = true; Rebuild();
    }
}
