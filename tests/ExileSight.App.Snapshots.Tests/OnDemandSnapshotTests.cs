using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ExileSight.App.Features;
using ExileSight.App.Features.Macros;
using ExileSight.App.Features.SearchStrings;
using ExileSight.App.Ui;
using ExileSight.Core.Features;
using ExileSight.Features.Omni;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class OnDemandSnapshotTests
{
    [WindowsOnlyFact]
    public void OmniActionsPrecedeLinks()
    {
#if SNAPSHOT_WINDOWS
        Render("on-demand-omni", 260, 230, theme =>
        {
            var model = new OmniMenuPresentation(new(1, 0, 0),
                [new("Wiki", "https://www.poewiki.net/"), new("Database", "https://poedb.tw/")],
                [new("Set as equipped", () => { }), new("Set listing price…", () => { })], "Storm Band");
            var card = OmniMenuBuilder.Build(model, theme);
            Assert.Equal(260, card.Width);
            var buttons = OnDemandControls.Buttons(card).ToArray();
            Assert.Equal(4, buttons.Length);
            Assert.All(buttons.Take(2), b => Assert.IsType<ItemMenuEntry>(b.Tag));
            Assert.All(buttons.Skip(2), b => Assert.IsType<OmniLink>(b.Tag));
            return card;
        });
#endif
    }

    [WindowsOnlyFact]
    public void WheelHighlightsDirectionWithoutExecuting()
    {
#if SNAPSHOT_WINDOWS
        Render("on-demand-wheel", 400, 300, theme =>
        {
            var selected = MacroWheelBuilder.Direction(new Point(300, 150));
            Assert.Equal(2, selected);
            Assert.Equal(-1, MacroWheelBuilder.Direction(new Point(200, 150)));
            var wheel = MacroWheelBuilder.Build(new(["/help", "/played", "/remaining", "/deaths",
                "/afk", "/dnd", "/passives", ""], selected), theme);
            var buttons = OnDemandControls.Buttons(wheel).Where(b => b.Tag is int).ToArray();
            Assert.Equal(8, buttons.Length);
            Assert.Same(theme.Accent, buttons[selected].BorderBrush);
            Assert.False(buttons[7].IsEnabled);
            wheel.Measure(new Size(400, 300));
            wheel.Arrange(new Rect(0, 0, 400, 300));
            for (var i = 0; i < buttons.Length; i++)
            {
                var current = buttons[i];
                var next = buttons[(i + 1) % buttons.Length];
                var bounds = new Rect(Canvas.GetLeft(current), Canvas.GetTop(current), current.ActualWidth, current.ActualHeight);
                var adjacent = new Rect(Canvas.GetLeft(next), Canvas.GetTop(next), next.ActualWidth, next.ActualHeight);
                Assert.False(bounds.IntersectsWith(adjacent), $"Wheel slots {i} and {(i + 1) % buttons.Length} overlap");
                var centre = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                Assert.Equal(100, (centre - new Point(200, 150)).Length, 6);
            }
            return wheel;
        });
#endif
    }

    [WindowsOnlyFact]
    public void SearchShowsSelectedVendorAndLinePosition()
    {
#if SNAPSHOT_WINDOWS
        Render("on-demand-search", 320, 230, theme =>
        {
            var card = SearchStringsBuilder.Build(new(["Vendor", "Stash", "Expedition"], "Vendor",
                [new("Armour bases", "\"armour\"", 1, 1), new("Socket colours", "\"b-b\"", 2, 3)]), theme);
            Assert.Equal(320, card.Width);
            var selected = OnDemandControls.Buttons(card).Single(b => Equals(b.Tag, "Vendor"));
            Assert.Same(theme.Accent, selected.BorderBrush);
            var row = (StackPanel)SearchStringsBuilder.Row(new("Socket colours", "\"b-b\"", 2, 3), theme);
            Assert.Equal("wheel ↕ 2/3", ((TextBlock)row.Children[1]).Text);
            return card;
        });
#endif
    }

    [WindowsOnlyFact]
    public void ButtonsShareTemplateAndAllowStyleTriggersToOverrideBaseBrush()
    {
#if SNAPSHOT_WINDOWS
        Render("on-demand-button-feedback", 320, 180, theme =>
        {
            var panel = new StackPanel();
            var buttons = new[]
            {
                OnDemandControls.Button(OnDemandControls.Row("In-app action", "›", theme), theme),
                OnDemandControls.Button(OnDemandControls.Row("Search", "text", theme, mono: true), theme),
                OnDemandControls.Button("Vendor", theme, pill: true),
                OnDemandControls.Button("Selected vendor", theme, pill: true, selected: true)
            };
            foreach (var button in buttons)
            {
                Assert.Same(buttons[0].Template, button.Template);
                Assert.True(button.Template.IsSealed);
                Assert.Equal(DependencyProperty.UnsetValue, button.ReadLocalValue(Control.BorderBrushProperty));
                var hover = Assert.Single(button.Style.Triggers.OfType<Trigger>(), t => t.Property == UIElement.IsMouseOverProperty);
                Assert.Equal(true, hover.Value);
                var setter = Assert.Single(hover.Setters.OfType<Setter>());
                Assert.Equal(Control.BorderBrushProperty, setter.Property);
                Assert.Same(theme.Accent, setter.Value);
                var normal = button.BorderBrush;
                button.SetValue(OnDemandControls.HighlightedProperty, true);
                Assert.Same(theme.Accent, button.BorderBrush);
                Assert.Equal(BaseValueSource.StyleTrigger,
                    DependencyPropertyHelper.GetValueSource(button, Control.BorderBrushProperty).BaseValueSource);
                button.SetValue(OnDemandControls.HighlightedProperty, false);
                Assert.Same(normal, button.BorderBrush);
                panel.Children.Add(button);
            }
            return panel;
        });
#endif
    }

#if SNAPSHOT_WINDOWS
    private static void Render(string name, int width, int height, Func<CardTheme, FrameworkElement> build)
    {
        var path = SnapshotRenderer.Render(() => build(CardTheme.Default), name, new Size(width, height));
        Assert.True(File.Exists(path));
        using var stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = Assert.Single(decoder.Frames);
        Assert.Equal(width, frame.PixelWidth);
        Assert.Equal(height, frame.PixelHeight);
    }
#endif
}
