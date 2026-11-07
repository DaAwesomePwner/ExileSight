using System.Reflection;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class ProductionNativeTests
{
    [Fact]
    public void OnlySendInputMayBeGeneratedInProduction()
    {
        foreach (var project in AssemblyInventory.Projects.Where(p => Repository.Relative(p).StartsWith("src/", StringComparison.OrdinalIgnoreCase)))
            AssemblyInventory.Read(AssemblyInventory.Output(project), reader =>
            {
                foreach (var typeHandle in reader.TypeDefinitions)
                    foreach (var methodHandle in reader.GetTypeDefinition(typeHandle).GetMethods())
                    {
                        var method = reader.GetMethodDefinition(methodHandle);
                        var name = reader.GetString(method.Name);
                        // Inspect every method and the native entry point too, regardless of wrapper/signature/type name.
                        var entry = (method.Attributes & MethodAttributes.PinvokeImpl) != 0
                            ? reader.GetString(method.GetImport().Name) : name;
                        Assert.False(Forbidden(Path.GetFileNameWithoutExtension(project), name, entry),
                            "Restricted API shipped in production: " + project + " :: " + name + " -> " + entry);
                    }
            });
        var platform = AssemblyInventory.Projects.Single(p => Path.GetFileName(p) == "ExileSight.Platform.Windows.csproj");
        var symbols = AssemblyInventory.Symbols(AssemblyInventory.Output(platform));
        Assert.Contains(symbols, id => id.StartsWith("M:Windows.Win32.PInvoke.GetWindowRect(", StringComparison.Ordinal));
        Assert.Contains("M:Windows.Win32.PInvoke.GetForegroundWindow", symbols);
    }

    internal static bool Forbidden(string assembly, string name, string entry) =>
        (BanCoverageTests.BannedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || entry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        && !(assembly == "ExileSight.Platform.Windows" && name == "SendInput" && entry == "SendInput");

    [Theory]
    [InlineData("ExileSight.Core", "SendInput", "SendInput")]
    [InlineData("ExileSight.Platform.Windows", "OpenProcess_SafeHandle", "OpenProcess")]
    [InlineData("ExileSight.App", "Disguised", "SendMessageW")]
    [InlineData("ExileSight.Features.Example", "InjectTouchInput", "InjectTouchInput")]
    [InlineData("ExileSight.Platform.Windows", "SendInputAlias", "SendInput")]
    public void RejectsVariantsAndAliasesInEveryProductionAssembly(string assembly, string name, string entry) =>
        Assert.True(Forbidden(assembly, name, entry));

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
