using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.Features.Guide;
using Microsoft.Win32;

namespace ExileSight.App.Features.Guide;

/// <summary>Desktop-only management. No timers, hotkeys or game actions.</summary>
public sealed class GuidePageHost : ContentControl
{
    private readonly GuideModel? model;
    private string? status;

    public GuidePageHost(GuideModel? model)
    {
        this.model = model;
        AddHandler(Button.ClickEvent, new RoutedEventHandler(Clicked));
        Refresh();
    }

    private void Refresh() => Content = GuidePageBuilder.Build(new(model?.StructuredSnapshot(), status), CardTheme.Default);

    private void Clicked(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is not Button { Tag: GuidePageAction action } || model is null) return;
        args.Handled = true;
        try
        {
            status = null;
            switch (action)
            {
                case GuidePageAction.Back: model.Navigate(-1); break;
                case GuidePageAction.Next: model.Navigate(1); break;
                case GuidePageAction.Reset: model.Reset(); break;
                case GuidePageAction.LeagueStart: model.Reset(changeLeague: true); break;
                case GuidePageAction.Bandit: model.Reset(changeBandit: true); break;
                case GuidePageAction.RemoveCustom: model.RemoveCustom(); break;
                case GuidePageAction.Import: Import(); break;
                case GuidePageAction.Export: Export(); break;
            }
        }
        catch (Exception e) when (e is RegexMatchTimeoutException or ArgumentException { InnerException: RegexMatchTimeoutException })
        { status = "Invalid guide: parsing exceeded the time limit."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { status = "Guide operation failed: " + e.GetType().Name; }
        Refresh();
    }

    private void Import()
    {
        var dialog = new OpenFileDialog { Filter = "Guide JSON|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        using var file = File.OpenRead(dialog.FileName);
        if (file.Length > GuideImport.MaxBytes) throw new ArgumentException("Guide exceeds size limit.");
        var bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes); model!.Import(bytes);
    }

    private void Export()
    {
        var csv = model!.Export();
        var dialog = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "guide-timers.csv", AddExtension = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) File.WriteAllText(dialog.FileName, csv);
    }
}
