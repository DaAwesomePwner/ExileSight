using ExileSight.App.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Spikes;

public static class InputSpike
{
    public static IServiceCollection AddInputSpike(this IServiceCollection services) => services.AddProductionInput();
    public static void StartInputSpike(IServiceProvider services) => InputRegistration.StartProductionInput(services);
}
