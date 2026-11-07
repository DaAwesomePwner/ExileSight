using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class BanCoverageTests
{
    internal static readonly string[] BannedPrefixes = ["SendInput", "keybd_event", "mouse_event", "PostMessage", "SendMessage",
        "SendMessageTimeout", "SendMessageCallback", "SendNotifyMessage", "PostThreadMessage", "OpenProcess", "ReadProcessMemory", "WriteProcessMemory", "VirtualAllocEx",
        "CreateRemoteThread", "Inject", "CreateSyntheticPointerDevice", "OpenThread", "DuplicateHandle", "TerminateProcess"];

    [Fact]
    public void GeneratedBannedMethodsHaveExactBanEntries()
    {
        var platform = AssemblyInventory.Projects.Single(p => Path.GetFileName(p) == "ExileSight.BanCoverage.Tests.csproj");
        var symbols = AssemblyInventory.Symbols(AssemblyInventory.Output(platform));
        var bans = Bans();
        var methods = symbols.Where(id => id.StartsWith("M:Windows.Win32.PInvoke.", StringComparison.Ordinal)).ToArray();
        foreach (var prefix in BannedPrefixes)
            Assert.Contains(methods, id => id.StartsWith("M:Windows.Win32.PInvoke." + prefix, StringComparison.Ordinal));
        foreach (var method in methods.Where(id => BannedPrefixes.Any(prefix => id.StartsWith("M:Windows.Win32.PInvoke." + prefix, StringComparison.Ordinal))))
            Assert.Contains(method, bans);

    }

    [Fact]
    public void BanEntriesResolveOrAreExplicitlySpeculative()
    {
        var platform = AssemblyInventory.Projects.Single(p => Path.GetFileName(p) == "ExileSight.BanCoverage.Tests.csproj");
        var symbols = AssemblyInventory.Symbols(AssemblyInventory.Output(platform));
        foreach (var assembly in new[] { typeof(Marshal).Assembly, typeof(Process).Assembly, typeof(System.Reflection.Emit.TypeBuilder).Assembly, typeof(System.Reflection.Emit.ILGenerator).Assembly }.Distinct())
            symbols.UnionWith(AssemblyInventory.Symbols(assembly.Location));
        var projection = File.ReadAllText(Path.Combine(Path.GetDirectoryName(AssemblyInventory.Output(platform))!, "WindowsProjection.path")).Trim();
        Assert.True(File.Exists(projection), "Missing Windows 19041 projection: " + projection);
        symbols.UnionWith(AssemblyInventory.Symbols(projection));
        Assert.Contains("T:Windows.UI.Input.Preview.Injection.InputInjector", symbols);
        foreach (var line in File.ReadLines(Path.Combine(Repository.Root, "BannedSymbols.txt")).Where(l => l.StartsWith("M:") || l.StartsWith("T:") || l.StartsWith("P:")))
        {
            var id = line.Split(';')[0];
            Assert.True(symbols.Contains(id) || line.Contains("SPECULATIVE:", StringComparison.Ordinal), "Unresolved ban: " + id);
        }
    }

    [Fact]
    public void ManagedEscapeHatchesAndProcessAccessAreBanned()
    {
        var bans = Bans();
        foreach (var type in new[] { "System.Runtime.InteropServices.NativeLibrary", "System.Reflection.Emit.DynamicMethod", "System.Windows.Forms.SendKeys", "Windows.UI.Input.Preview.Injection.InputInjector" })
            Assert.Contains("T:" + type, bans);
        var symbols = AssemblyInventory.Symbols(typeof(Marshal).Assembly.Location);
        symbols.UnionWith(AssemblyInventory.Symbols(typeof(Process).Assembly.Location));
        symbols.UnionWith(AssemblyInventory.Symbols(typeof(System.Reflection.Emit.TypeBuilder).Assembly.Location));
        symbols.UnionWith(AssemblyInventory.Symbols(typeof(System.Reflection.Emit.ILGenerator).Assembly.Location));
        foreach (var id in symbols.Where(id => id.StartsWith("M:System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer", StringComparison.Ordinal)
            || id.StartsWith("M:System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate", StringComparison.Ordinal)
            || id.StartsWith("M:System.Diagnostics.Process.Kill", StringComparison.Ordinal)
            || id.StartsWith("M:System.Diagnostics.Process.CloseMainWindow", StringComparison.Ordinal)
            || id.StartsWith("M:System.Diagnostics.Process.WaitForExit", StringComparison.Ordinal)
            || id.StartsWith("M:System.Diagnostics.Process.set_PriorityClass", StringComparison.Ordinal)
            || id.StartsWith("M:System.Diagnostics.Process.set_ProcessorAffinity", StringComparison.Ordinal)
            || id.StartsWith("M:System.Reflection.Emit.TypeBuilder.DefinePInvokeMethod", StringComparison.Ordinal)
            || id.StartsWith("M:System.Reflection.Emit.ILGenerator.EmitCalli", StringComparison.Ordinal)))
            Assert.Contains(id, bans);
        foreach (var property in new[] { "Handle", "SafeHandle", "MainModule", "Modules", "HasExited", "PriorityClass", "ProcessorAffinity" })
            Assert.Contains("P:System.Diagnostics.Process." + property, bans);
    }

    private static HashSet<string> Bans() => File.ReadLines(Path.Combine(Repository.Root, "BannedSymbols.txt"))
        .Where(l => l.Length > 2 && l[1] == ':').Select(l => l.Split(';')[0]).ToHashSet(StringComparer.Ordinal);
}
