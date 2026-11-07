using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class DependencyTests
{
    [Fact]
    public void ProductionAssembliesRespectBoundaries()
    {
        foreach (var project in AssemblyInventory.Projects.Where(Repository.IsProduction))
        {
            var name = Path.GetFileNameWithoutExtension(project);
            var xml = XDocument.Load(project);
            if (AssemblyInventory.Portable(name))
                Assert.Equal("net10.0", xml.Descendants("TargetFramework").Single().Value);
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "UseWindowsForms" && e.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
            AssemblyInventory.Read(AssemblyInventory.Output(project), reader =>
            {
                foreach (var handle in reader.AssemblyReferences)
                {
                    var reference = reader.GetString(reader.GetAssemblyReference(handle).Name);
                    Assert.True(AllowedEdge(name, reference), $"Forbidden assembly edge {name} -> {reference}");
                }
                if (!name.Equals("ExileSight.Platform.Windows", StringComparison.OrdinalIgnoreCase))
                    foreach (var handle in reader.MethodDefinitions)
                        Assert.False((reader.GetMethodDefinition(handle).Attributes & MethodAttributes.PinvokeImpl) != 0,
                            $"P/Invoke in {name}: {reader.GetString(reader.GetMethodDefinition(handle).Name)}");
            });
        }
    }

    [Fact]
    public void ProjectReferencesRespectBoundaries()
    {
        foreach (var project in AssemblyInventory.Projects)
        {
            var name = Path.GetFileNameWithoutExtension(project);
            var xml = XDocument.Load(project);
            foreach (var reference in xml.Descendants().Where(e => e.Name.LocalName is "ProjectReference" or "Reference"))
            {
                var target = (reference.Attribute("Include")?.Value ?? "").Replace('\\', '/').Split('/').Last().Split(',')[0];
                if (target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || target.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    target = Path.GetFileNameWithoutExtension(target);
                Assert.True(AllowedEdge(name, target), $"Forbidden project edge {name} -> {target}");
            }
            if (AssemblyInventory.Portable(name))
                Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "UseWPF" && e.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
        }
    }

    internal static bool AllowedEdge(string source, string target)
    {
        source = source.ToLowerInvariant();
        target = target.ToLowerInvariant();
        if (target == "exilesight.platform.windows")
            return source is "exilesight.app" or "exilesight.platform.windows.tests";
        if (new[] { "presentationframework", "presentationcore", "windowsbase", "system.xaml" }.Contains(target))
            return source == "exilesight.app";
        if (!target.StartsWith("exilesight.", StringComparison.Ordinal))
            return true;
        if (source.EndsWith(".tests", StringComparison.Ordinal))
            return true;
        if (target == "exilesight.platform.abstractions")
            return true;
        if (target == "exilesight.core")
            return source is "exilesight.app" or "exilesight.platform.windows" or "exilesight.datagen" or "exilesight.redactlog"
                || source.StartsWith("exilesight.features.", StringComparison.Ordinal);
        return source == "exilesight.app" && target.StartsWith("exilesight.features.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ExileSight.Core", "ExileSight.Features.Test")]
    [InlineData("ExileSight.DataGen", "ExileSight.Platform.Windows")]
    [InlineData("ExileSight.RedactLog", "ExileSight.Platform.Windows")]
    [InlineData("ExileSight.RedactLog", "PresentationFramework")]
    [InlineData("ExileSight.RedactLog", "PresentationCore")]
    [InlineData("ExileSight.RedactLog", "WindowsBase")]
    [InlineData("ExileSight.RedactLog", "System.Xaml")]
    [InlineData("EXILESIGHT.CORE", "WINDOWSBASE")]
    [InlineData("ExileSight.Platform.Abstractions", "ExileSight.Core")]
    [InlineData("ExileSight.Features.One", "ExileSight.Features.Two")]
    public void RejectsInvalidEdges(string source, string target) => Assert.False(AllowedEdge(source, target));

    [Fact]
    public void EveryFeatureDeclaresAManifest()
    {
        foreach (var project in AssemblyInventory.Projects.Where(p => AssemblyInventory.Portable(Path.GetFileNameWithoutExtension(p))
            && Path.GetFileName(p).StartsWith("ExileSight.Features.", StringComparison.OrdinalIgnoreCase)))
        {
            var sources = Repository.Files("*.cs").Where(p => p.StartsWith(Path.GetDirectoryName(project)! + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText).ToArray();
            Assert.Contains(sources, text => text.Contains("IFeature", StringComparison.Ordinal));
            Assert.Contains(sources, text => text.Contains("FeatureManifest", StringComparison.Ordinal));
        }
    }
}
