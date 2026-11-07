using System.Windows;
using System.Windows.Controls;
using ExileSight.Core.Pricing;
using ExileSight.Features.Vaalasync;

namespace ExileSight.App.Features.Vaalasync;

public sealed class HistoryView : Window
{
    private readonly ShopModel model;
    private readonly PriceService prices;
    private readonly StackPanel rows = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    public HistoryView(ShopModel model, PriceService prices)
    {
        this.model = model; this.prices = prices;
        Title = "Shop history — whispers do not confirm sales"; Width = 700; Height = 650;
        var panel = new StackPanel { Margin = new Thickness(12) };
        var custom = new TextBox { Text = model.Custom };
        panel.Children.Add(new TextBlock { Text = "Custom amount: save here, return to the game, open its note box, click Apply custom." });
        panel.Children.Add(custom);
        var save = new Button { Content = "Save custom amount" };
        save.Click += (_, _) =>
        {
            if (ShopNote.ParseAmount(custom.Text) is null) { status.Text = "Invalid amount."; return; }
            model.Custom = custom.Text; status.Text = "Saved. Return to the game and open its note box before clicking Apply custom.";
        };
        panel.Children.Add(save);
        var item = new TextBox(); var note = new TextBox { Text = "~b/o 1 chaos" };
        panel.Children.Add(new TextBlock { Text = "Record a completed purchase: item and price note" });
        panel.Children.Add(item); panel.Children.Add(note);
        var purchase = new Button { Content = "Record purchase locally" };
        purchase.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(item.Text) || ShopNote.Parse(note.Text) is not { } parsed) { status.Text = "Enter item and valid price."; return; }
            try { model.History.RecordPurchase(item.Text, model.League, parsed, DateTimeOffset.UtcNow); await Refresh(); }
            catch (Exception error) { status.Text = "Could not save: " + error.GetType().Name; }
        };
        panel.Children.Add(purchase); panel.Children.Add(status);
        var refresh = new Button { Content = "Refresh history and rates" };
        refresh.Click += async (_, _) => await Refresh(); panel.Children.Add(refresh);
        panel.Children.Add(rows); Content = new ScrollViewer { Content = panel };
        Loaded += async (_, _) => await Refresh();
    }
    private async Task Refresh()
    {
        try
        {
            var snapshot = model.History.Snapshot();
            var rates = new Dictionary<string, PriceResult>();
            foreach (var league in snapshot.Select(r => r.League).Distinct())
                rates[league] = await prices.GetAsync(league, "Currency", TimeSpan.FromHours(1));
            rows.Children.Clear();
            foreach (var row in snapshot.Reverse())
            {
                rows.Children.Add(new TextBlock { Text = $"{row.Item} ({row.Base}, ilvl {row.ItemLevel}) · {row.League} · " +
                    (row.Purchase ? "Purchased" : row.Sold is null ? "Listed" : $"Sold {row.Sold:g}") +
                    (row.Evidence is null ? "" : " · Whisper evidence (uncertain)"), TextWrapping = TextWrapping.Wrap });
                foreach (var entry in row.Prices)
                    rows.Children.Add(new TextBlock { Text = $"{entry.Time:g} {entry.Kind}: {entry.Note} — {Conversions.Describe(entry.Note, rates[row.League])}", TextWrapping = TextWrapping.Wrap });
                if (!row.Purchase && row.Sold is null)
                {
                    var sold = new Button { Content = "Mark sold manually" };
                    sold.Click += async (_, _) =>
                    {
                        try { model.History.MarkSold(row.Id, DateTimeOffset.UtcNow); await Refresh(); }
                        catch (Exception error) { status.Text = "Could not save: " + error.GetType().Name; }
                    };
                    rows.Children.Add(sold);
                }
            }
        }
        catch (Exception error) { status.Text = "History/rates unavailable: " + error.GetType().Name; }
    }
}
