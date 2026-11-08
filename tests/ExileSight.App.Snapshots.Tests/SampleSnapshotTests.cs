using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class SampleSnapshotTests
{
    [WindowsOnlyFact]
    public void SampleCardWritesPngAtBaselineScale()
    {
#if SNAPSHOT_WINDOWS
        var path = SnapshotRenderer.Render(() =>
        {
            Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            return new Border
            {
                Background = Brushes.Black,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Child = new TextBlock
                {
                    Text = "Snapshot harness",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 16,
                    Foreground = Brushes.White
                }
            };
        }, "sample-card", new Size(320, 120));

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
        using var stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = Assert.Single(decoder.Frames);
        Assert.Equal(320, frame.PixelWidth);
        Assert.Equal(120, frame.PixelHeight);
        Assert.Equal(96, frame.DpiX, 1);
        Assert.Equal(96, frame.DpiY, 1);
#endif
    }
}
