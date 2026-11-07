using System.Reflection;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class ProductionNativeTests
{
    [Fact]
    public void OnlySendInputMayBeGeneratedInProduction()
    {
        var bans = File.ReadLines(Path.Combine(Repository.Root, "BannedSymbols.txt"))
            .Where(line => line.StartsWith("M:", StringComparison.Ordinal)).Select(line => line.Split(';')[0]).ToHashSet();
        var project = AssemblyInventory.Projects.Single(p => Path.GetFileName(p) == "ExileSight.Platform.Windows.csproj");
        AssemblyInventory.Read(AssemblyInventory.Output(project), reader =>
        {
            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(typeHandle);
                foreach (var methodHandle in type.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    if ((method.Attributes & MethodAttributes.PinvokeImpl) == 0)
                        continue;
                    var id = MetadataSignatures.MethodId(reader, type, method);
                    Assert.True(!bans.Contains(id) || reader.GetString(method.Name) == "SendInput", "Restricted API shipped in production: " + id);
                }
            }
        });
        var symbols = AssemblyInventory.Symbols(AssemblyInventory.Output(project));
        Assert.Contains(symbols, id => id.StartsWith("M:Windows.Win32.PInvoke.GetWindowRect(", StringComparison.Ordinal));
        Assert.Contains("M:Windows.Win32.PInvoke.GetForegroundWindow", symbols);
    }

    [Fact]
    public void NoProductionReferenceCanReachTestInventory()
    {
        foreach (var project in AssemblyInventory.Projects.Where(Repository.IsProduction))
            AssemblyInventory.Read(AssemblyInventory.Output(project), reader =>
            {
                foreach (var handle in reader.AssemblyReferences)
                    Assert.DoesNotContain(".Tests", reader.GetString(reader.GetAssemblyReference(handle).Name), StringComparison.OrdinalIgnoreCase);
            });
    }
}
