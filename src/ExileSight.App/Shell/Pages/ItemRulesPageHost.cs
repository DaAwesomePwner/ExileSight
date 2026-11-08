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
    private readonly Task<string[]> loading;
    private string[] translations = [];
    private ItemRulesEditor editor = new(ItemInfoSettings.Default);
    private OmniSettings omni = OmniSettings.Default;
    private bool building;
    public bool Dirty { get; private set; }
    public ItemRulesPageHost(ISettingsStore<SettingsDocument> store, ItemInfoViewModel? source, string[] translations)
        : this(store, source, Task.FromResult(translations)) { }
    public ItemRulesPageHost(ISettingsStore<SettingsDocument> store, ItemInfoViewModel? source, Task<string[]> translations)
    {
        this.store = store; this.source = source; loading = translations;
        Reload();
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(Changed));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(Changed));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(Changed));
        AddHandler(Button.ClickEvent, new RoutedEventHandler(Clicked));
        Loaded += (_, _) => { if (source is not null) source.PropertyChanged += SourceChanged; UpdatePreview(); };
        Unloaded += (_, _) => { if (source is not null) source.PropertyChanged -= SourceChanged; };
        _ = LoadSearch();
    }
    private async Task LoadSearch()
    {
        try { translations = await loading; Search(); }
        catch (Exception error)
        { Find<TextBlock>("SearchStatus").Text = "Mod search unavailable. " + error.GetType().Name; }
    }
    private T Find<T>(string name) where T : FrameworkElement =>
        ShellViewState.Find<T>(Content as DependencyObject).First(e => e.Name == name);
    private void SourceChanged(object? sender, PropertyChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(UpdatePreview));
    private void Reload()
    {
        var document = store.Snapshot;
        editor = new(ItemInfoSettings.Load(new OverlaySettingsContext(document, "iteminfo")));
        omni = OmniSettings.Load(new OverlaySettingsContext(document, "omni"));
        Rebuild(); Dirty = false;
    }
    private void Rebuild()
    {
        building = true;
        Content = ItemRulesPageBuilder.Build(new(editor.Settings, omni, EditingProfile: editor.Editing, Rows: editor.Rows),
            CardTheme.Default);
        building = false; Search(); UpdatePreview();
    }
    private void Capture()
    {
        editor.Capture(Find<StackPanel>("RuleRows").Children.OfType<WrapPanel>().Select(panel =>
        {
            var row = (ItemRulesRow)panel.Tag;
            var match = panel.Children.OfType<RadioButton>().FirstOrDefault(b => b.IsChecked == true)?.Tag;
            return row with { Classes = panel.Children.OfType<TextBox>().Single().Text,
                Match = match is Highlight h ? h : row.Match };
        }));
        bool On(string name) => Find<CheckBox>(name).IsChecked == true;
        editor.Settings = editor.Settings with { ShowCompare = On("Compare"), ShowDefenceBasePercentile = On("Defence"),
            ShowItemLevelColumn = On("Level"), ShowImplicits = On("Implicits") };
        omni = new(On("Advanced"));
    }
    private void Search()
    {
        Find<TextBox>("RuleSearch").IsEnabled = loading.IsCompletedSuccessfully;
        Find<TextBlock>("SearchStatus").Text = !loading.IsCompleted ? "Loading mod search…" :
            loading.IsFaulted ? "Mod search unavailable." : translations.Length == 0 ? "No stats translations available." : "";
        var query = Find<TextBox>("RuleSearch").Text.Trim();
        Find<ListBox>("RuleResults").ItemsSource = query.Length < 2 ? [] : translations
            .Where(text => RuleText.Display(text).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(100).Select(text => new ItemRulesSearchResult(text)).ToArray();
    }
    private void Changed(object sender, RoutedEventArgs e)
    {
        if (building) return;
        if (e.OriginalSource is TextBox { Name: "RuleSearch" }) { Search(); return; }
        Capture(); Dirty = true; UpdatePreview();
    }
    private void UpdatePreview()
    {
        try
        {
            var settings = editor.Snapshot(preview: true);
            // Read the atomically published pair once.
            var snapshot = source?.LastCopy;
            var result = snapshot?.Result;
            var card = result is null ? null : ItemCardBuilder.Build(result with
            {
                Settings = settings,
                Mods = result.Mods.Select(m => m with { Highlight = settings.Evaluate(result.Item, m.Source) }).ToArray()
            }, settings: settings, comparison: snapshot?.Comparison);
            Find<ContentControl>("ItemPreview").Content = ItemRulesPageBuilder.Preview(card, CardTheme.Default);
            Find<TextBlock>("RulesNotice").Text = "";
        }
        catch (ArgumentException error) { Find<TextBlock>("RulesNotice").Text = error.Message; }
    }
    private void Clicked(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button { Tag: string action } ||
            !(action is "SaveRules" or "DiscardRules" or "AddRule" or "ActivateRules" ||
              action.StartsWith("Profile", StringComparison.Ordinal) || action.StartsWith("Remove", StringComparison.Ordinal))) return;
        e.Handled = true;
        if (action == "DiscardRules") { Reload(); return; }
        Capture();
        if (action == "SaveRules")
        {
            try
            {
                var settings = editor.Snapshot();
                store.Update("iteminfo", document =>
                {
                    settings.Save(new OverlaySettingsContext(document, "iteminfo"));
                    omni.Save(new OverlaySettingsContext(document, "omni"));
                });
                Dirty = false; Find<TextBlock>("RulesNotice").Text = "Changes saved.";
            }
            catch (Exception error) when (error is ArgumentException or System.IO.IOException or InvalidOperationException or System.Text.Json.JsonException)
            { Find<TextBlock>("RulesNotice").Text = "Not saved. " + error.Message; }
            return;
        }
        if (action.StartsWith("Profile", StringComparison.Ordinal))
        {
            editor.Editing = int.Parse(action[7..], System.Globalization.CultureInfo.InvariantCulture);
            Rebuild(); return;
        }
        if (action == "ActivateRules") editor.Activate();
        else if (action == "AddRule")
        {
            if (Find<ListBox>("RuleResults").SelectedItem is not ItemRulesSearchResult selected) return;
            editor.Add(selected.Template);
        }
        else editor.Remove(Guid.Parse(action[6..]));
        Dirty = true; Rebuild();
    }
}
