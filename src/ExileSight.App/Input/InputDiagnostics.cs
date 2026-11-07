using ExileSight.Core.Diagnostics;
using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.App.Input;

public sealed class InputDiagnostics(IActionGate gate, IClock clock, HotkeyConfiguration configuration) : IDebugSection
{
    private readonly object sync = new();
    private IHotkeys? hooks;
    private string lastHotkey = "None";
    private string lastOutcome = "None";
    private bool auditFailed;
    private string copiedText = "";
    public string Title => "Input";
    public void Start(IHotkeys input)
    {
        hooks = input;
        try { input.Start(); }
        catch (Exception error) { lock (sync) lastOutcome = $"Hook startup failed: {error.Message}"; }
    }
    public void Report(HotkeyBinding binding, ActionResult result)
    {
        lock (sync) lastHotkey = $"{binding.Id} — {clock.UtcNow:O}";
        lock (sync)
        {
            auditFailed = result.AuditFailed;
            lastOutcome = $"{result.Outcome}: {result.Reason}";
            if (result.CopiedText is { } text) copiedText = text;
        }
    }
    public IReadOnlyList<KeyValuePair<string, string>> Snapshot()
    {
        lock (sync)
            return [
                new("Hook thread alive", (hooks?.IsAlive == true).ToString()),
                new("Bindings", string.Join("\n", configuration.ActiveBindings)),
                new("Rejected bindings", string.Join("\n", configuration.RejectedBindings)),
                new("Current contexts", string.Join(", ", configuration.ActiveContexts)),
                new("Last hotkey / UTC", lastHotkey),
                new("Last outcome", lastOutcome),
                new("Last copied item", string.Join("\n", copiedText.Split('\n').Take(15))),
                new("Audit log", gate.AuditPath),
                new("Audit failed", auditFailed.ToString()),
                new("Actions sent", gate.ActionsSent.ToString()),
                new("Clipboard preservation", "Phase 0: Unicode text or empty only")
            ];
    }
}
