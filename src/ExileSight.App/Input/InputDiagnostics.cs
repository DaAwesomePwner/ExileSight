using ExileSight.Core.Diagnostics;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.App.Input;

public sealed class InputDiagnostics(IActionGate gate, IClock clock) : IDebugSection
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
    public void Execute(IntentToken token, HotkeyBinding binding)
    {
        lock (sync) lastHotkey = $"{binding.Id} — {clock.UtcNow:O}";
        var result = gate.TryExecute(token, binding.Action);
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
                new("Bindings (game only)", "F7: copy item; F8: /hideout (blocked)"),
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
