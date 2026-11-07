using ExileSight.Core.Diagnostics;
using ExileSight.Core.Overlay;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Overlay;

public sealed class OverlayController : IDebugSection, IDisposable
{
    private readonly IGameWindow game;
    private readonly IProcessStats stats;
    private readonly IOverlayWindowStyler styler;
    private readonly object gate = new();
    private bool visible;
    private SpikeOverlayWindow? window;
    public string Title => "Overlay";

    public OverlayController(IGameWindow game, IProcessStats stats, IOverlayWindowStyler styler)
    {
        this.game = game;
        this.stats = stats;
        this.styler = styler;
    }

    public void Start()
    {
        if (window is not null) return;
        window = new SpikeOverlayWindow(styler);
        window.InitializeHandle();
        game.Changed += Update;
        game.Start();
        Update(this, EventArgs.Empty);
    }

    private void Update(object? sender, EventArgs e)
    {
        window?.Update(game.Current);
        lock (gate) visible = window?.IsVisible == true;
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
                ["Scale factor"] = OverlayRules.Scale(state.ClientRect.Height).ToString("F3"),
                ["IsForeground"] = state.IsForeground.ToString(),
                ["IsMinimized"] = state.IsMinimized.ToString(),
                ["Overlay visible"] = visible.ToString(),
                ["Last event time"] = state.LastEventTime.ToString("O"),
                ["Own working set MB"] = (sample.WorkingSetBytes / 1048576d).ToString("F1"),
                ["Own private MB"] = (sample.PrivateBytes / 1048576d).ToString("F1"),
                ["Own CPU % (all cores)"] = sample.CpuPercent.ToString("F2")
            }.ToArray();
    }

    public void Dispose()
    {
        game.Changed -= Update;
        game.Dispose();
        window?.Close();
        window = null;
    }
}
