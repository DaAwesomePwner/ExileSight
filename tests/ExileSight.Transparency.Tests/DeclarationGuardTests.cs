using ExileSight.Core.Features;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Transparency.Tests;

public sealed class DeclarationGuardTests
{
    [Fact]
    public void FeatureProjectsEqualAppRegistrations()
    {
        var root = TransparencyTests.FindRoot();
        FeatureInventory.AssertRegisteredSet(root, FeatureInventory.Types(root));
    }

    [Fact]
    public void AnUnregisteredFeatureCannotAppearInTheTable()
    {
        var root = TransparencyTests.FindRoot();
        var types = FeatureInventory.Types(root).Append(typeof(ConstructorFeature)).ToArray();
        var error = Assert.Throws<InvalidOperationException>(() => FeatureInventory.AssertRegisteredSet(root, types));
        Assert.Contains("differ from App CompiledModules", error.Message);
        Assert.Contains(typeof(ConstructorFeature).FullName!, error.Message);
    }

    [Fact]
    public void FeatureWriteApisHaveWriteDeclarations()
    {
        var root = TransparencyTests.FindRoot();
        foreach (var type in FeatureInventory.Types(root))
        {
            var feature = FeatureInventory.Compose(type, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
            var directory = Path.Combine(root, "src", type.Assembly.GetName().Name!);
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                         .Where(f => !Path.GetRelativePath(directory, f).Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj")))
                WriteSource.AssertDeclared(feature.Manifest.Id, Path.GetRelativePath(root, file), File.ReadAllText(file), DeclaredResources.FeatureWrites);
        }
    }

    [Theory]
    [InlineData("File.WriteAllText(path, value);")]
    [InlineData("System.IO.File.Move(a, b);")]
    [InlineData("File.Replace(a, b, null);")]
    [InlineData("File.Create(path);")]
    [InlineData("new FileStream(path, FileMode.CreateNew);")]
    [InlineData("new FileStream(path, FileMode.Open, FileAccess.Write);")]
    [InlineData("Directory.CreateDirectory(path);")]
    [InlineData("WriteAtomic(path, bytes);")]
    [InlineData("store?.Save(state);")]
    [InlineData("files.Write(path, bytes);")]
    [InlineData("context.WriteSetting(key, value);")]
    public void NewWritesCannotRemainUndeclared(string source)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            WriteSource.AssertDeclared("example", "Example.cs", source, new Dictionary<string, string[]> { ["example"] = [] }));
        Assert.Contains("Example.cs", error.Message);
    }

    [Fact]
    public void ReadOnlyStreamsDoNotRequireWrites() =>
        Assert.False(WriteSource.HasWrite("new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);"));

    [Fact]
    public void UnattributedBindingsFailClearly()
    {
        var error = Assert.Throws<InvalidOperationException>(() => TransparencyTable.AssertBindingOwners(["omni"],
            [new FeatureDefaultBinding("orphan", 0x14, KeyModifiers.None, true, "absent:press")]));
        Assert.Contains("orphan", error.Message);
        Assert.Contains("absent:press", error.Message);
    }

    [Fact]
    public void ConstructorFailuresExplainPortableComposition()
    {
        var error = Assert.Throws<InvalidOperationException>(() => FeatureInventory.Compose(typeof(ConstructorFeature), "unused"));
        Assert.Contains(typeof(ConstructorFeature).FullName!, error.Message);
        Assert.Contains("ComposeCore", error.Message);
        Assert.IsType<MissingMethodException>(error.InnerException);
    }

    private sealed class ConstructorFeature(string unused) : IFeature
    {
        public FeatureManifest Manifest => throw new NotSupportedException(unused);
        public void Register(IFeatureContext context) => throw new NotSupportedException();
    }
}
