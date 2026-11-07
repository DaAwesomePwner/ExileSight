using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class DependencyTests
{
    private static IEnumerable<string> Projects => Repository.Files("*.csproj")
        .Where(p => Repository.Relative(p).StartsWith("src/", StringComparison.Ordinal));

    [Fact]
    public void PortableAssembliesHaveNoWindowsDependencies()
    {
        foreach (var project in Projects.Where(IsPortable))
        {
            var name = Path.GetFileNameWithoutExtension(project);
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var outputs = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(project)!, "bin", configuration),
                name + ".dll", SearchOption.AllDirectories);
            Assert.NotEmpty(outputs);
            foreach (var output in outputs)
            {
                using var stream = File.OpenRead(output);
                using var pe = new PEReader(stream);
                var metadata = pe.GetMetadataReader();
                foreach (var handle in metadata.AssemblyReferences)
                {
                    var reference = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
                    Assert.DoesNotContain(reference, new[] { "PresentationFramework", "PresentationCore",
                        "WindowsBase", "System.Xaml", "ExileSight.Platform.Windows" });
                }
            }
        }
    }

    [Fact]
    public void OnlyAppReferencesWindowsPlatform()
    {
        foreach (var project in Projects)
        {
            var xml = XDocument.Load(project);
            var references = xml.Descendants().Where(e => e.Name.LocalName is "ProjectReference" or "Reference")
                .Select(e => e.Attribute("Include")?.Value ?? "");
            if (Path.GetFileNameWithoutExtension(project) != "ExileSight.App")
                Assert.DoesNotContain(references, r => r.Contains("ExileSight.Platform.Windows", StringComparison.Ordinal));
            if (IsPortable(project))
            {
                Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "UseWPF" && e.Value == "true");
                Assert.DoesNotContain(references, r => new[] { "PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml" }
                    .Any(forbidden => r.Contains(forbidden, StringComparison.Ordinal)));
            }
        }
    }

    [Fact]
    public void EveryFeatureDeclaresAManifest()
    {
        // Phase 0 has no IFeature contract yet. Future feature projects must supply it and a manifest.
        foreach (var project in Projects.Where(p => Path.GetFileName(p).StartsWith("ExileSight.Features.", StringComparison.Ordinal)))
        {
            var sources = Directory.GetFiles(Path.GetDirectoryName(project)!, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Split(Path.DirectorySeparatorChar).Contains("obj"))
                .Select(File.ReadAllText).ToArray();
            Assert.Contains(sources, text => text.Contains("IFeature", StringComparison.Ordinal));
            Assert.Contains(sources, text => text.Contains("FeatureManifest", StringComparison.Ordinal)
                && text.Contains("Manifest", StringComparison.Ordinal));
        }
    }

    private static bool IsPortable(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name is "ExileSight.Core" or "ExileSight.Platform.Abstractions"
            || name.StartsWith("ExileSight.Features.", StringComparison.Ordinal);
    }
}
