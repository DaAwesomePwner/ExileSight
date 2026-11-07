using ExileSight.Core.Diagnostics;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Overlay;

public sealed class OverlayController : IDebugSection, IDisposable
{
    private readonly IGameWindow game;
    private readonly IProcessStats stats;
    private readonly OverlayHost host;
    private HostedOverlayWindow? status;
    private readonly object gate = new();
    private bool visible;
    private double scale;
    private string? lastError;
    public string Title => "Overlay";

    public OverlayController(IGameWindow game, IProcessStats stats, OverlayHost host)
    {
        this.game = game;
        this.stats = stats;
        this.host = host;
    }

    public void Start()
    {
        if (status is not null) return;
        var label = new System.Windows.Controls.TextBlock
        {
            Text = "ExileSight running", Padding = new System.Windows.Thickness(6)
        };
        label.SetResourceReference(System.Windows.Controls.TextBlock.BackgroundProperty, "OverlayBackground");
        status = host.Register(label, new(8, 8, 160, 32));
        host.Start();
        host.Changed += Update;
        Update(this, EventArgs.Empty);
    }

    public void SetStatusVisible(bool value)
    {
        if (status is not null) host.SetVisible(status, value);
        Update(this, EventArgs.Empty);
    }

    private void Update(object? sender, EventArgs e)
    {
        lock (gate) { visible = host.IsVisible; lastError = host.LastError; scale = host.EffectiveScale; }
    }

    public IReadOnlyList<KeyValuePair<string, string>> Snapshot()
    {
        var state = game.Current;
        var sample = stats.Sample();
        lock (gate)
            return new Dictionary<string, string>
            {
                ["Found"] = state.Found ? "yes" : "no",
                ["Game HWND"] = $"0x{state.Handle:X}",
                ["Client rect (physical)"] = state.ClientRect.ToString(),
                ["DPI"] = state.Dpi.ToString(),
                ["Scale factor"] = scale.ToString("F3"),
                ["IsForeground"] = state.IsForeground.ToString(),
                ["IsMinimized"] = state.IsMinimized.ToString(),
                ["Overlay visible"] = visible.ToString(),
                ["Overlay last error"] = lastError ?? "none",
                ["Tracker last error"] = game.LastError ?? "none",
                ["Last event time"] = state.LastEventTime.ToString("O"),
                ["Own working set MB"] = (sample.WorkingSetBytes / 1048576d).ToString("F1"),
                ["Own private MB"] = "unavailable (handle-free sampling)",
                ["Own CPU % (all cores)"] = sample.CpuPercent.ToString("F2")
            }.ToArray();
    }

    public void Dispose()
    {
        host.Changed -= Update;
        host.Dispose();
    }
}
