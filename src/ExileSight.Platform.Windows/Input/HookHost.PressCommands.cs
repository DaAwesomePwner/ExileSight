using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Platform.Windows.Input;

public sealed partial class HookHost
{
    private IPressCommandHandler? FindPressCommand(HotkeyBinding binding) =>
        binding.FeatureCommand is { } id && featureCommands.TryGetValue(id, out var command)
        && command.FeatureId == binding.FeatureId ? command as IPressCommandHandler : null;
}
