using ExileSight.App.Features;
using ExileSight.Core.Input;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Features.Stashninja;

public sealed class CalibrationFlow(PassiveOverlayInput passive, IGameWindow game, CalibrationStore store)
{
    public void Cancel() => passive.Close(this);
    public void Start(Layout layout, Action<string> status, Action completed)
    {
        Cancel();
        var initial = game.Current.ClientRect;
        var resolution = new Resolution(initial.Width, initial.Height);
        var first = layout.Cells[0];
        var a = Geometry.Center(layout, first);
        var second = layout.Cells.OrderByDescending(c =>
        { var p = Geometry.Center(layout, c); return Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2); }).First();
        Point? click = null;
        status($"Click center of {first.ItemName ?? first.NinjaId}; then {second.ItemName ?? second.NinjaId}. Esc cancels.");
        passive.Open(this, input =>
        {
            if (input.Message == 0x100) { Cancel(); status("Calibration cancelled"); return; }
            if (input.Message != 0x201) return;
            var current = game.Current;
            if (!current.IsForeground || current.ClientRect != initial)
            { Cancel(); status("Client changed; restart calibration"); return; }
            var point = new Point(input.X - initial.X, input.Y - initial.Y);
            if (point.X < 0 || point.Y < 0 || point.X >= initial.Width || point.Y >= initial.Height) return;
            if (click is null) { click = point; status($"Click center of {second.ItemName ?? second.NinjaId}"); return; }
            Cancel();
            try
            {
                var transform = Geometry.Calibrate(a, Geometry.Center(layout, second), click, point);
                store.Save(layout, resolution, transform);
                status("Calibration saved for this tab/page and resolution"); completed();
            }
            catch (Exception e) when (e is ArgumentException or System.IO.IOException or UnauthorizedAccessException)
            { status("Calibration could not be saved: " + e.Message); }
        });
    }
}
