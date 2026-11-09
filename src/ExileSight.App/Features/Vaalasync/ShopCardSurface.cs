using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ExileSight.App.Ui;
using ExileSight.Features.Vaalasync;

namespace ExileSight.App.Features.Vaalasync;

/// <summary>Persistent controls: refreshing presentation never detaches a gesture's target.</summary>
public sealed class ShopCardSurface
{
    private readonly CardTheme theme;
    private readonly ContentControl header = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly ContentControl hints = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly UniformGrid amounts = new() { Columns = 4 };
    private readonly Dictionary<string, Button> actions = [];
    public Border Frame { get; }
    public IReadOnlyDictionary<string, Button> Actions => actions;
    public Button EditCustom { get; }
    public Button History { get; }
    public Button Close { get; }

    public ShopCardSurface(ShopCardModel model, CardTheme theme, Action<Button, string>? bind = null)
    {
        this.theme = theme;
        var body = new StackPanel();
        body.Children.Add(header);
        foreach (var (choice, _) in ShopCardMapping.Choices)
        {
            var button = ShopCardBuilder.Button(new TextBlock(), choice, theme);
            button.Height = 56;
            button.Margin = new Thickness(0, 0, 6, 0);
            actions.Add(choice, button);
            amounts.Children.Add(button);
        }
        body.Children.Add(amounts);
        EditCustom = ShopCardBuilder.Button(CardPrimitives.Text("Custom amount…", theme), "edit-custom", theme);
        EditCustom.Margin = new Thickness(0, 10, 0, 0);
        body.Children.Add(EditCustom);
        var custom = ShopCardBuilder.Button(new TextBlock(), "custom", theme);
        custom.Margin = new Thickness(0, 6, 0, 0);
        actions.Add("custom", custom);
        body.Children.Add(custom);
        body.Children.Add(hints);
        var footer = new UniformGrid { Columns = 2, Margin = new Thickness(0, 8, 0, 0) };
        History = ShopCardBuilder.Button(CardPrimitives.Text("History", theme, "TextSecondary", 11), "history", theme);
        History.Margin = new Thickness(0, 0, 6, 0);
        Close = ShopCardBuilder.Button(CardPrimitives.Text("Close", theme, "TextSecondary", 11), "close", theme);
        footer.Children.Add(History);
        footer.Children.Add(Close);
        body.Children.Add(footer);
        Frame = CardPrimitives.CardFrame(body, theme);
        Frame.Width = 330;
        Update(model);
        foreach (var (choice, button) in actions) bind?.Invoke(button, choice);
    }

    /// <summary>Attach before resolving command ownership; retain the old card if any binding fails.</summary>
    public static ShopCardSurface Replace(Panel parent, ShopCardSurface? previous, ShopCardModel model,
        CardTheme theme, Action<Button, string> bind)
    {
        var next = new ShopCardSurface(model, theme);
        parent.Children.Add(next.Frame);
        try
        {
            foreach (var (choice, button) in next.Actions) bind(button, choice);
        }
        catch
        {
            parent.Children.Remove(next.Frame);
            throw;
        }
        if (previous is not null) parent.Children.Remove(previous.Frame);
        return next;
    }

    public void Update(ShopCardModel model)
    {
        header.Content = ShopCardBuilder.Header(model, theme);
        hints.Content = ShopCardBuilder.Hints(model, theme);
        amounts.Margin = new Thickness(0, model.Pasted is null ? 0 : 8, -6, 0);
        foreach (var amount in model.Amounts)
        {
            var button = actions[amount.Choice];
            button.Content = ShopCardBuilder.Amount(amount, model.NoteMode, theme);
            button.IsEnabled = model.Armed && amount.Available;
        }
        EditCustom.IsEnabled = model.Armed;
        EditCustom.Visibility = model.Pasted is null ? Visibility.Visible : Visibility.Collapsed;
        var custom = actions["custom"];
        custom.Content = CardPrimitives.Text("Apply custom · " + model.CustomAmount, theme);
        custom.IsEnabled = model.Armed && model.CustomAmount is not null;
        custom.Visibility = model.Pasted is null && model.CustomAmount is not null ? Visibility.Visible : Visibility.Collapsed;
    }
}
