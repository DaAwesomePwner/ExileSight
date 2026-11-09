using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ExileSight.Features.Lab;

namespace ExileSight.App.Features.Lab;

internal static class LabMap
{
    public static void Draw(Canvas canvas, LabLayout layout, string? current)
    {
        canvas.Children.Clear();
        var minX = layout.Rooms.Min(r => r.X); var minY = layout.Rooms.Min(r => r.Y);
        var spanX = Math.Max(1, layout.Rooms.Max(r => r.X) - minX);
        var spanY = Math.Max(1, layout.Rooms.Max(r => r.Y) - minY);
        Point Position(LabRoom r) => new(25 + (r.X - minX) / spanX * 375, 20 + (r.Y - minY) / spanY * 210);
        var rooms = layout.Rooms.ToDictionary(r => r.Id);
        foreach (var room in layout.Rooms)
            foreach (var id in room.Exits.Values)
            {
                var a = Position(room); var b = Position(rooms[id]);
                canvas.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = Brushes.Gray, StrokeThickness = 2 });
            }
        foreach (var room in layout.Rooms)
        {
            var point = Position(room);
            var marker = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
                Background = room.Id == current ? Brushes.LimeGreen : Brushes.SlateGray,
                BorderBrush = Brushes.White, BorderThickness = new Thickness(room.Id == current ? 3 : 1),
                ToolTip = room.Name + " (" + room.Id + ")\n" + string.Join(", ", room.Contents),
                Child = new TextBlock { Text = room.Id, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center } };
            Canvas.SetLeft(marker, point.X - 12); Canvas.SetTop(marker, point.Y - 12); canvas.Children.Add(marker);
            var label = new TextBlock { Text = string.Join(" ", room.Contents.Select(Icon)), FontSize = 12,
                Foreground = Brushes.Gold, ToolTip = string.Join(", ", room.Contents) };
            Canvas.SetLeft(label, point.X - 12); Canvas.SetTop(label, point.Y + 12); canvas.Children.Add(label);
        }
    }
    private static string Icon(string content) => content.ToLowerInvariant() switch
    {
        "darkshrine" => "◆", "golden-key" => "K", "golden-door" => "D", "argus" => "A",
        var s when s.Contains("trap") || s.Contains("gauntlet") => "⚠", _ => "•"
    };
}
