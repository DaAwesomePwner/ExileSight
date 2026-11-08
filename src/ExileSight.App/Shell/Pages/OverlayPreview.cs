using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExileSight.App.Ui;
using static ExileSight.App.Shell.ShellElements;
namespace ExileSight.App.Shell.Pages;

public static class OverlayPreview
{
    public static readonly RoutedEvent PositionChangedEvent = EventManager.RegisterRoutedEvent(
        "PositionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(OverlayPreview));
    public static void CompleteDrag(Canvas canvas, Point original)
    {
        if ((Point)canvas.Tag != original) canvas.RaiseEvent(new RoutedEventArgs(PositionChangedEvent));
    }
    public static Canvas Create(OverlayPageModel model, CardTheme theme)
    {
        var canvas = new Canvas { Name = "DockPreview", Width = 430, Height = 430 / model.AspectRatio,
            Background = theme.AppNav, ClipToBounds = true, Margin = new Thickness(0, 0, 0, 16),
            HorizontalAlignment = HorizontalAlignment.Left, Tag = new Point(model.Settings.DockedX, model.Settings.DockedY) };
        var sample = new Border { Width = 100, Height = Math.Min(54, canvas.Height), Background = theme.OverlayRaised,
            BorderBrush = theme.Accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Child = Text("Sample card\nDrag to position", theme, 12), Cursor = Cursors.SizeAll, Focusable = true };
        canvas.Children.Add(sample);
        void Position(Point fraction)
        {
            sample.Height = Math.Min(54, canvas.Height);
            Canvas.SetLeft(sample, Math.Clamp(fraction.X * canvas.Width, 0, canvas.Width - sample.Width));
            Canvas.SetTop(sample, Math.Clamp(fraction.Y * canvas.Height, 0, canvas.Height - sample.Height));
        }
        Position((Point)canvas.Tag);
        canvas.SizeChanged += (_, _) => Position((Point)canvas.Tag);
        Point offset = default, original = default;
        sample.MouseLeftButtonDown += (_, e) => { original = (Point)canvas.Tag; offset = e.GetPosition(sample); sample.Focus(); sample.CaptureMouse(); e.Handled = true; };
        sample.MouseMove += (_, e) =>
        {
            if (!sample.IsMouseCaptured) return;
            var point = e.GetPosition(canvas);
            var fraction = new Point(Math.Clamp((point.X - offset.X) / canvas.Width, 0, 1 - sample.Width / canvas.Width),
                Math.Clamp((point.Y - offset.Y) / canvas.Height, 0, 1 - sample.Height / canvas.Height));
            canvas.Tag = fraction; Position(fraction);
        };
        sample.LostMouseCapture += (_, _) => CompleteDrag(canvas, original);
        sample.MouseLeftButtonUp += (_, e) => { sample.ReleaseMouseCapture(); e.Handled = true; };
        return canvas;
    }
}
