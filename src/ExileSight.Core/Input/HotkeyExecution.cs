using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Input;

/// <summary>Runs on the queue consumer, never on the hook thread.</summary>
public sealed class HotkeyExecution(IActionGate gate, IEnumerable<IFeatureCommandHandler> handlers)
{
    public ActionResult Execute(IntentToken? token, HotkeyBinding binding)
    {
        if (binding.Action is { } action) return gate.TryExecute(token, action);
        var handler = handlers.FirstOrDefault(h => h.Command == binding.FeatureCommand);
        return handler?.Execute() ?? new(ActionOutcome.Failed, "Feature command is no longer registered");
    }
}
