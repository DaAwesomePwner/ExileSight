using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class InteropMetadataTests
{
    [Fact]
    public void MetadataDetectsNativeMethodsWithoutLoadingOrCallingThem()
    {
        var fixture = AssemblyInventory.Projects.Single(p => Path.GetFileName(p) == "ExileSight.BanCoverage.Tests.csproj");
        using var stream = File.OpenRead(AssemblyInventory.Output(fixture));
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        Assert.Contains(reader.MethodDefinitions, handle =>
            (reader.GetMethodDefinition(handle).Attributes & MethodAttributes.PinvokeImpl) != 0);
    }
}
