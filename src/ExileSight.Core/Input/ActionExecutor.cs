using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Input;

public sealed class ActionExecutor : IActionGate
{
    private readonly object sync = new();
    private readonly GatePolicy policy;
    private readonly IKeySender sender;
    private readonly IClock clock;
    private readonly IActionAudit audit;
    private readonly ActionTransaction transaction;
    public ActionExecutor(GatePolicy policy, IClipboard clipboard, IKeySender sender, IClock clock, IActionAudit audit)
    {
        this.policy = policy; this.sender = sender; this.clock = clock; this.audit = audit;
        transaction = new(clipboard, sender, clock);
    }
    public string AuditPath => audit.Path;
    public int ActionsSent => transaction.ActionsSent;
    public ActionResult TryExecute(IntentToken? token, GameAction action)
    {
        lock (sync)
        {
            var foreground = false;
            try { foreground = sender.IsGameForeground; }
            catch (Exception) { /* Fail closed; still consume the token and audit the refusal. */ }
            var verdict = policy.Validate(token, action, foreground, out var press);
            ActionResult result = new(verdict, verdict == ActionOutcome.Success ? "" : verdict.ToString());
            if (verdict == ActionOutcome.Success)
            {
                try { audit.EnsureWritable(); }
                catch (Exception) { result = new(ActionOutcome.Failed, "Audit unavailable before sending", AuditFailed: true); }
                if (!result.AuditFailed)
                {
                    try { result = transaction.Execute(action, press!); }
                    catch (Exception) { result = new(ActionOutcome.Failed, "Input preparation failed"); }
                }
            }
            try { audit.Append(ActionData.Audit(clock.UtcNow, press?.HotkeyId, action, result.Outcome, result.Reason)); }
            catch (Exception) { result = result with { AuditFailed = true }; }
            return result; // audit failure never overwrites execution outcome or copied text
        }
    }
}
