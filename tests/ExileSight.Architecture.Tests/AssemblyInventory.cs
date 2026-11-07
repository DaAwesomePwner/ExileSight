using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Xunit;

namespace ExileSight.Architecture.Tests;

internal static class AssemblyInventory
{
    internal static IEnumerable<string> Projects => Repository.Files("*.csproj");
    internal static bool Portable(string name) => name.ToLowerInvariant() is "exilesight.core" or "exilesight.platform.abstractions" or "exilesight.datagen"
        || name.StartsWith("ExileSight.Features.", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase);

    internal static string Output(string project)
    {
        var tfm = XDocument.Load(project).Descendants("TargetFramework").Single().Value;
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var path = Path.Combine(Path.GetDirectoryName(project)!, "bin", configuration, tfm, Path.GetFileNameWithoutExtension(project) + ".dll");
        Assert.True(File.Exists(path), $"Expected assembly missing: {path}. Build the entire solution in {configuration} before running architecture tests.");
        return path;
    }

    internal static void Read(string path, Action<MetadataReader> inspect)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        inspect(pe.GetMetadataReader());
    }

    internal static HashSet<string> Symbols(string path)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        Read(path, reader =>
        {
            foreach (var handle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(handle);
                var name = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
                result.Add("T:" + name);
                // Only these types contribute method bans; avoid unrelated function-pointer signatures.
                if (name is not ("Windows.Win32.PInvoke" or "System.Runtime.InteropServices.Marshal" or "System.Diagnostics.Process" or "System.Reflection.Emit.TypeBuilder" or "System.Reflection.Emit.ILGenerator"))
                    continue;
                foreach (var methodHandle in type.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    if (name != "Windows.Win32.PInvoke" && (method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask)
                        is not (System.Reflection.MethodAttributes.Public or System.Reflection.MethodAttributes.Family or System.Reflection.MethodAttributes.FamORAssem))
                        continue;
                    result.Add(MetadataSignatures.MethodId(reader, type, method));
                }
                foreach (var property in type.GetProperties())
                    result.Add("P:" + name + "." + reader.GetString(reader.GetPropertyDefinition(property).Name));
            }
        });
        return result;
    }
}
