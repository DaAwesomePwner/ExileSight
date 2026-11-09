#if SNAPSHOT_WINDOWS
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ExileSight.App.Snapshots.Tests;

public static class SnapshotRenderer
{
    /// <summary>
    /// Creates and renders a view on its own STA at 96 DPI (1080p scale 1.0).
    /// Omit logicalSize to measure to content. The factory must create all WPF objects on this thread.
    /// </summary>
    public static string Render(Func<FrameworkElement> factory, string outputName, Size? logicalSize = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (string.IsNullOrWhiteSpace(outputName) || outputName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || outputName.Contains('/') || outputName.Contains('\\') || outputName is "." or "..")
            throw new ArgumentException("Use a unique file name without directories.", nameof(outputName));
        if (logicalSize is { } requested)
            ValidateSize(requested);

        var directory = Environment.GetEnvironmentVariable("EXILESIGHT_UI_SNAPSHOTS_DIR");
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "ui-snapshots");
        var path = Path.GetFullPath(Path.Combine(directory,
            outputName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? outputName : outputName + ".png"));
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var element = factory();
                element.UseLayoutRounding = true;
                element.Measure(logicalSize ?? new Size(double.PositiveInfinity, double.PositiveInfinity));
                var size = logicalSize ?? element.DesiredSize;
                ValidateSize(size);
                element.Arrange(new Rect(new Point(), size));
                element.UpdateLayout();
                // Finish queued bindings and layout before capturing the detached visual.
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var bitmap = new RenderTargetBitmap(checked((int)Math.Ceiling(size.Width)),
                    checked((int)Math.Ceiling(size.Height)), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(element);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var stream = File.Create(path);
                encoder.Save(stream);
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
        return path;
    }

    private static void ValidateSize(Size size)
    {
        if (!double.IsFinite(size.Width) || !double.IsFinite(size.Height) || size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Snapshot dimensions must be finite and positive.");
    }
}
#endif
