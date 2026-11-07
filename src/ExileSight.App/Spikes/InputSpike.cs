using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Spikes;

public static class InputSpike
{
    public static IServiceCollection AddInputSpike(this IServiceCollection s) => s;

    public static void StartInputSpike(IServiceProvider sp) { }
}
