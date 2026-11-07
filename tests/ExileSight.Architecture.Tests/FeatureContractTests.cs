using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class FeatureContractTests
{
    [Fact]
    public void CompiledFeaturesExposeOneValidManifestAndDeclareReferencedActions()
    {
        var projects = AssemblyInventory.Projects.Where(Repository.IsProduction).ToArray();
        var paths = projects.ToDictionary(p => Path.GetFileNameWithoutExtension(p)!, AssemblyInventory.Output);
        var context = new AssemblyLoadContext("feature-contract-audit", isCollectible: true);
        context.Resolving += (_, name) => paths.TryGetValue(name.Name!, out var path) ? context.LoadFromAssemblyPath(path) : null;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var project in projects.Where(p => Path.GetFileName(p).StartsWith("ExileSight.Features.", StringComparison.Ordinal)))
            {
                var assembly = context.LoadFromAssemblyPath(AssemblyInventory.Output(project));
                var type = Assert.Single(assembly.GetExportedTypes(), t => !t.IsAbstract && t.GetInterfaces()
                    .Any(i => i.FullName == "ExileSight.Core.Features.IFeature"));
                // Modules must be cheap, parameterless registration objects; resolve services during composition.
                var feature = Activator.CreateInstance(type)!;
                var manifest = type.GetProperty("Manifest")!.GetValue(feature)!;
                manifest.GetType().GetMethod("Validate")!.Invoke(manifest, null);
                Assert.True(ids.Add((string)manifest.GetType().GetProperty("Id")!.GetValue(manifest)!));
                var actions = ((IEnumerable)manifest.GetType().GetProperty("GameActions")!.GetValue(manifest)!)
                    .Cast<object>().Select(a => a.ToString()!).ToHashSet();
                var attribute = Assert.Single(type.GetCustomAttributes(), a => a.GetType().FullName ==
                    "ExileSight.Core.Features.DeclaredActionsAttribute");
                var declared = ((IEnumerable)attribute.GetType().GetProperty("Actions")!.GetValue(attribute)!)
                    .Cast<object>().Select(a => a.ToString()!).ToHashSet();
                Assert.True(actions.SetEquals(declared), "DeclaredActions must equal the manifest");
                foreach (var file in Repository.Files("*.cs").Where(p => p.StartsWith(Path.GetDirectoryName(project)! + Path.DirectorySeparatorChar)))
                    foreach (Match match in Regex.Matches(File.ReadAllText(file), @"GameAction\.(CopyItem|SendChatCommand|PasteSearch|SetPriceNote)\b"))
                        Assert.Contains(match.Groups[1].Value, actions);
            }
        }
        finally { context.Unload(); }
    }
}
