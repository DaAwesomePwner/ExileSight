using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using System.Windows.Documents;
using System.Windows.Shapes;
using ExileSight.Features.Lab;

namespace ExileSight.App.Features.Lab;

internal static class LabMap
{
    public static void Draw(Canvas canvas, LabLayout layout, string? current, CardTheme theme)
    {
        canvas.Children.Clear();
        var minX = layout.Rooms.Min(r => r.X); var minY = layout.Rooms.Min(r => r.Y);
        var spanX = Math.Max(1, layout.Rooms.Max(r => r.X) - minX);
        var spanY = Math.Max(1, layout.Rooms.Max(r => r.Y) - minY);
        Point Position(LabRoom r) => new(25 + (r.X - minX) / spanX * 360, 20 + (r.Y - minY) / spanY * 210);
        var rooms = layout.Rooms.ToDictionary(r => r.Id);
        foreach (var room in layout.Rooms)
            foreach (var id in room.Exits.Values)
            {
                var a = Position(room); var b = Position(rooms[id]);
                canvas.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = theme.OverlayBorder, StrokeThickness = 2 });
            }
        foreach (var room in layout.Rooms)
        {
            var point = Position(room);
            var marker = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
                Background = room.Id == current ? theme.OverlayAccent : theme.OverlayRaised,
                BorderBrush = theme.TextSecondary, BorderThickness = new Thickness(room.Id == current ? 3 : 1),
                ToolTip = room.Name + " (" + room.Id + ")\n" + string.Join(", ", room.Contents),
                Child = CardPrimitives.Text(room.Id == current ? "◎" + room.Id : room.Id, theme,
                    room.Id == current ? "AppNav" : "OverlayForeground", theme.NumberSize, true) };
            Canvas.SetLeft(marker, point.X - 12); Canvas.SetTop(marker, point.Y - 12); canvas.Children.Add(marker);
            var label = CardPrimitives.Text("", theme, "TextSecondary", theme.LabelSize);
            label.ToolTip = string.Join(", ", room.Contents);
            foreach (var item in room.Contents)
                label.Inlines.Add(new Run(Icon(item) + " ") { Foreground = theme[Colour(item)] });
            Canvas.SetLeft(label, point.X - 12); Canvas.SetTop(label, point.Y + 12); canvas.Children.Add(label);
        }
    }
    private static string Colour(string content) => content.ToLowerInvariant() switch
    {
        "darkshrine" => "TooltipCrafted", "golden-key" or "golden-door" => "TooltipUnique",
        "argus" => "TooltipHigh",
        var s when s.Contains("trap") || s.Contains("gauntlet") => "TooltipLow", _ => "TextSecondary"
    };
    private static string Icon(string content) => content.ToLowerInvariant() switch
    {
        "darkshrine" => "◆", "golden-key" => "K", "golden-door" => "D", "argus" => "A",
        var s when s.Contains("trap") || s.Contains("gauntlet") => "⚠", _ => "•"
    };
}
