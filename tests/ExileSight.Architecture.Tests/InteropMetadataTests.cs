using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class InteropMetadataTests
{
    [Fact]
    public void MetadataDetectsNativeMethodsWithoutLoadingOrCallingThem()
    {
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("NativeBoundaryFixture"), typeof(object).Assembly);
        var module = assembly.DefineDynamicModule("Fixture");
        var type = module.DefineType("Fixture");
        type.DefinePInvokeMethod("Forbidden", "not-a-real-library", MethodAttributes.Public | MethodAttributes.Static,
            CallingConventions.Standard, typeof(void), Type.EmptyTypes, CallingConvention.Winapi, CharSet.Unicode);
        type.CreateType();
        using var stream = new MemoryStream();
        assembly.Save(stream);
        stream.Position = 0;
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        Assert.Contains(reader.MethodDefinitions, handle =>
            (reader.GetMethodDefinition(handle).Attributes & MethodAttributes.PinvokeImpl) != 0);
    }
}
