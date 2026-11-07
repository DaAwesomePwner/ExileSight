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
        var types = Types(root);
        AssertRegisteredSet(root, types);
        var features = types.Select(type => Compose(type, scratch)).ToArray();
        return new(features, (_, _) => null, (_, _, _) =>
            throw new InvalidOperationException("Transparency generation must not write feature settings."),
            dispatch: action => action());
    }

    internal static Type[] Types(string root) => Directory.GetFiles(Path.Combine(root, "src"), "ExileSight.Features.*.csproj",
                SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(project => Assembly.Load(Path.GetFileNameWithoutExtension(project)))
            .Select(assembly => assembly.GetExportedTypes().Single(t => !t.IsAbstract && typeof(IFeature).IsAssignableFrom(t)))
            .ToArray();

    internal static void AssertRegisteredSet(string root, IReadOnlyList<Type> types)
    {
        var registered = RegistrationSource.Read(root, types).Order(StringComparer.Ordinal).ToArray();
        var discovered = types.Select(t => t.FullName!).Order(StringComparer.Ordinal).ToArray();
        if (discovered.Length == 0 || !discovered.SequenceEqual(registered))
            throw new InvalidOperationException("Feature projects differ from App CompiledModules. " +
                "Register/remove the feature or update portable composition.\nProjects: " + string.Join(", ", discovered) +
                "\nApp registrations: " + string.Join(", ", registered));
    }

    internal static IFeature Compose(Type type, string scratch)
    {
        try { return ComposeCore(type, scratch); }
        catch (Exception error)
        {
            throw new InvalidOperationException($"Cannot instantiate feature {type.FullName}. " +
                "Update FeatureInventory.ComposeCore with portable constructor dependencies; do not reference App/WPF.", error);
        }
    }

    private static IFeature ComposeCore(Type type, string scratch)
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
