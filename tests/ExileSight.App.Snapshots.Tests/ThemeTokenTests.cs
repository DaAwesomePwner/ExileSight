using Xunit;
#if SNAPSHOT_WINDOWS
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExileSight.App.Ui;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class ThemeTokenTests
{
    [WindowsOnlyFact]
    public void AllThemeEntryPointsExposeSpecTokens()
    {
#if SNAPSHOT_WINDOWS
        SnapshotRenderer.Render(() =>
        {
            var expected = new Dictionary<string, string>
            {
                ["OverlayForeground"] = "#E8EAED",
                ["OverlayBackground"] = "#0F1217",
                ["OverlayAccent"] = "#6FE3D5",
                ["TooltipBest"] = "#6FE3D5",
                ["TooltipHigh"] = "#6AA8FF",
                ["TooltipMedium"] = "#AEB6C2",
                ["TooltipLow"] = "#F2994A",
                ["TooltipCrafted"] = "#C3A6FF",
                ["TooltipUnique"] = "#E2B97F",
                ["TooltipUnknown"] = "#5C6573",
                ["OverlayRaised"] = "#17202A",
                ["OverlayBorder"] = "#2E3540",
                ["OverlayRule"] = "#262C35",
                ["TextSecondary"] = "#B7BEC8",
                ["TextMuted"] = "#8E97A4",
                ["TextFaint"] = "#7D8592",
                ["Danger1"] = "#E6CC5A",
                ["Danger2"] = "#F2994A",
                ["Danger3"] = "#FF6B5E",
                ["Accent"] = "#6FD1C6",
                ["AppBackground"] = "#0F1216",
                ["AppPanel"] = "#14181E",
                ["AppNav"] = "#0C0F12",
                ["StatTileBackground"] = "#161A21",
                ["RollTrack"] = "#2A313C",
                ["DangerRowBackground"] = "#221616",
            };
            foreach (var source in new[] { "Overlay/OverlayTheme.xaml", "Features/ItemInfo/OverlayTheme.xaml", "Ui/AppTheme.xaml" })
            {
                var dictionary = new ResourceDictionary
                {
                    Source = new Uri("/ExileSight.App;component/" + source, UriKind.Relative)
                };
                var theme = CardTheme.FromDictionary(dictionary);
                foreach (var (key, value) in expected)
                {
                    var brush = Assert.IsType<SolidColorBrush>(dictionary[key]);
                    Assert.Equal((Color)ColorConverter.ConvertFromString(value), brush.Color);
                    Assert.True(brush.IsFrozen);
                    Assert.Equal(brush.Color, theme[key].Color);
                    Assert.Equal(brush.Color, CardTheme.Default[key].Color);
                    Assert.Same(CardTheme.Default[key], CardTheme.Default[key]);
                }
                Assert.Equal(.96, theme["OverlayBackground"].Opacity);
                Assert.Equal("Segoe UI Variable, Segoe UI", theme.TextFont.Source);
                Assert.Equal("Cascadia Mono, Consolas", theme.NumberFont.Source);
            }
            return new Border { Width = 1, Height = 1 };
        }, "theme-validation");
#endif
    }
}

