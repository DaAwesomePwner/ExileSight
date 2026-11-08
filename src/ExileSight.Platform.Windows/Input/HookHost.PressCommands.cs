using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Platform.Windows.Input;

public sealed partial class HookHost
{
    internal void RouteAndReport(ExileSight.Core.Input.PhysicalPress press)
    {
        var result = router.Route(press);
        if (result.Reason.StartsWith("Copy timed out", StringComparison.Ordinal))
            ExileSight.Core.Input.InputTiming.Record(ExileSight.Core.Input.InputStage.CopyWaitTimeout, 0,
                ExileSight.Core.Input.InputTimingOutcome.Timeout);
        if (router.ShouldReport(press, result)) report(press.Binding, result);
    }
    private IPressCommandHandler? FindPressCommand(HotkeyBinding binding) =>
        binding.FeatureCommand is { } id && featureCommands.TryGetValue(id, out var command)
        && command.FeatureId == binding.FeatureId ? command as IPressCommandHandler : null;
}
