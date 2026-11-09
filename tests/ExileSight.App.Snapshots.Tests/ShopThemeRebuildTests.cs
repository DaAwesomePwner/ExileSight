using Xunit;
#if SNAPSHOT_WINDOWS
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Features.Vaalasync;
using ExileSight.App.Ui;
using ExileSight.Core.Settings;
using ExileSight.Features.Vaalasync;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class ShopThemeRebuildTests
{
    [WindowsOnlyFact]
    public void ThemeReplacementParentsEveryActionBeforeBinding()
    {
#if SNAPSHOT_WINDOWS
        SnapshotRenderer.Render(() =>
        {
            var parent = new StackPanel();
            var window = new Window { Content = parent };
            var bound = new List<Button>();
            var invoked = new List<string>();
            void Bind(Button button, string choice)
            {
                // Runtime FeatureViews.Bind resolves ownership through this window ancestry.
                Assert.Same(window, Window.GetWindow(button));
                bound.Add(button);
                button.Click += (_, _) => invoked.Add(choice);
            }
            var original = ShopCardSurface.Replace(parent, null, Fixture(), CardTheme.Default, Bind);
            Assert.Equal(5, bound.Count);
            bound.Clear();
            var theme = OverlayTheme.From(new OverlaySettings
            { Density = OverlayDensity.Compact, Contrast = OverlayContrast.HighContrast });
            var replacement = ShopCardSurface.Replace(parent, original, Fixture(), theme, Bind);
            Assert.NotSame(original, replacement);
            Assert.Same(replacement.Frame, Assert.Single(parent.Children.Cast<UIElement>()));
            Assert.Null(original.Frame.Parent);
            Assert.Same(theme.OverlayBorder, replacement.Frame.BorderBrush);
            Assert.Equal(5, bound.Count);
            foreach (var (choice, button) in replacement.Actions)
            {
                Assert.Contains(button, bound);
                Assert.NotSame(original.Actions[choice], button);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            Assert.Equal(replacement.Actions.Keys, invoked);
            window.Content = null;
            window.Close();
            return parent;
        }, "shop-theme-rebound");
#endif
    }

    [WindowsOnlyFact]
    public void FailedRebindPreservesOldCardAndAllowsRetry()
    {
#if SNAPSHOT_WINDOWS
        SnapshotRenderer.Render(() =>
        {
            var parent = new StackPanel();
            var window = new Window { Content = parent };
            var original = ShopCardSurface.Replace(parent, null, Fixture(), CardTheme.Default, (_, _) => { });
            var theme = CardTheme.For(OverlayDensity.Compact, OverlayContrast.HighContrast);
            var attempts = 0;
            Assert.Throws<ArgumentException>(() => ShopCardSurface.Replace(parent, original, Fixture(), theme, (button, _) =>
            {
                Assert.Same(window, Window.GetWindow(button));
                Assert.Contains(original.Frame, parent.Children.Cast<UIElement>());
                if (++attempts == 2) throw new ArgumentException("Binding rejected");
            }));
            Assert.Equal(2, attempts);
            Assert.Same(original.Frame, Assert.Single(parent.Children.Cast<UIElement>()));
            Assert.Same(CardTheme.Default.OverlayBorder, original.Frame.BorderBrush);
            var rebound = 0;
            var replacement = ShopCardSurface.Replace(parent, original, Fixture(), theme, (button, _) =>
            { Assert.Same(window, Window.GetWindow(button)); rebound++; });
            Assert.Equal(5, rebound);
            Assert.Same(replacement.Frame, Assert.Single(parent.Children.Cast<UIElement>()));
            Assert.Same(theme.OverlayBorder, replacement.Frame.BorderBrush);
            window.Content = null;
            window.Close();
            return parent;
        }, "shop-theme-rebind-retry");
#endif
    }

#if SNAPSHOT_WINDOWS
    private static ShopCardModel Fixture() => new("Astral Plate", "40 divine", TimeSpan.FromHours(29),
        "divine", true, false, [new("minus10", "−10%", "36", true), new("minus1", "−1", "39", true),
            new("plus1", "+1", "41", true), new("plus10", "+10%", "44", true)]) { CustomAmount = "12 divine" };
#endif
}
