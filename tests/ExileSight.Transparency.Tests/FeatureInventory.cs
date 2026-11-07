using System.Reflection;
using ExileSight.Core.Features;
using ExileSight.Features.GearCmp;
using ExileSight.Features.MapInfo;
using ExileSight.Features.MapTracker;
using ExileSight.Features.Timers;
using ExileSight.Features.Vaalasync;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Transparency.Tests;

internal static class FeatureInventory
{
    // Discovery is test-only: production still explicitly composes compiled-in modules.
    internal static FeatureHost Read(string root, string scratch)
    {
        var features = Directory.GetFiles(Path.Combine(root, "src"), "ExileSight.Features.*.csproj",
                SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(project => Assembly.Load(Path.GetFileNameWithoutExtension(project)))
            .Select(assembly => assembly.GetExportedTypes().Single(t => !t.IsAbstract && typeof(IFeature).IsAssignableFrom(t)))
            .Select(type => Compose(type, scratch)).ToArray();
        if (features.Length == 0) throw new InvalidOperationException("No feature projects found.");
        return new(features, (_, _) => null, (_, _, _) =>
            throw new InvalidOperationException("Transparency generation must not write feature settings."),
            dispatch: action => action());
    }

    private static IFeature Compose(Type type, string scratch)
    {
        // Only storage-dependent modules need portable composition; no App/WPF, network or input.
        var directory = Path.Combine(scratch, type.Name);
        if (type == typeof(GearCmpFeature)) return new GearCmpFeature(new(new(directory)));
        if (type == typeof(MapInfoFeature)) return new MapInfoFeature(new(new(directory)));
        if (type == typeof(MapTrackerFeature)) return new MapTrackerFeature(new(new RunStore(directory)));
        if (type == typeof(TimersFeature)) return new TimersFeature(new(new(directory), new SystemClock()));
        if (type == typeof(VaalasyncFeature)) return new VaalasyncFeature(new(new(directory), () => "Standard"));
        return (IFeature)Activator.CreateInstance(type)!;
    }
}
