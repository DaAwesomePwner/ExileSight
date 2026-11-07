using System.Text.Json;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class CompilerGuardTests
{
    [Fact]
    public async Task EvaluatedCompilerInputsCannotDisableTheBoundary()
    {
        var directory = Path.Combine(Repository.Root, "src/ExileSight.Core/obj", "guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var project = Path.Combine(directory, "GuardProbe.csproj");
            await File.WriteAllTextAsync(project, """
                <Project>
                  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework><NuGetAudit>false</NuGetAudit></PropertyGroup>
                  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
                  <Import Project="Mutation.targets" />
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(directory, "Probe.cs"), "namespace GuardProbe; public sealed class Probe { public void Inspect(System.Diagnostics.Process p) { p.Refresh(); _ = p.Id; } }");
            await File.WriteAllTextAsync(Path.Combine(directory, "policy.txt"), "is_global = true\ndotnet_diagnostic.RS0030.severity = none\n");
            await File.WriteAllTextAsync(Path.Combine(directory, "compiler.rsp"), "/skipanalyzers+\n/nowarn:RS0030\n");
            File.Copy(typeof(CompilerGuardTests).Assembly.Location, Path.Combine(directory, "ExtraAnalyzer.dll"));
            var mutationFile = Path.Combine(directory, "Mutation.targets");
            await File.WriteAllTextAsync(mutationFile, "<Project />");
            using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository.Root, "tests/ExileSight.Architecture.Tests/obj/project.assets.json")));
            var cache = assets.RootElement.GetProperty("packageFolders").EnumerateObject().First().Name;
            var restore = await Run(directory, "restore", project, "--source", cache, "-p:NuGetAudit=false", "-nr:false");
            Assert.True(restore.ExitCode == 0, restore.Output);
            var baseline = await Run(directory, "build", project, "--no-restore", "-nr:false");
            Assert.True(baseline.ExitCode == 0, baseline.Output);
            foreach (var (mutation, expected) in Cases())
            {
                // Imported AFTER SDK targets; this mutation runs after the pre-compile guard.
                await File.WriteAllTextAsync(mutationFile,
                    "<Project><Target Name=\"MutateCompilerInputs\" BeforeTargets=\"CoreCompile\">" + mutation + "</Target></Project>");
                var result = await Run(directory, "build", project, "--no-restore", "-nr:false");
                Assert.True(result.ExitCode != 0 && result.Output.Contains("ExileSight compiler boundary: " + expected, StringComparison.Ordinal),
                    mutation + Environment.NewLine + result.Output);
            }
            await File.WriteAllTextAsync(mutationFile, "<Project />");
            await File.WriteAllTextAsync(Path.Combine(directory, "Probe.cs"), """
                using System;
                using System.Diagnostics;
                using System.Reflection;
                using System.Reflection.Emit;
                using System.Runtime.InteropServices;
                namespace GuardProbe;
                public static class Probe
                {
                    public static void Check(Process p, TypeBuilder type, ILGenerator il)
                    {
                        p.PriorityClass = ProcessPriorityClass.Normal;
                        p.ProcessorAffinity = (nint)1;
                        _ = p.HasExited;
                        _ = p.ProcessName;
                        _ = Process.GetProcessById(1);
                        p.WaitForExit();
                        _ = p.WaitForExitAsync();
                        _ = new DynamicMethod("x", typeof(void), Type.EmptyTypes);
                        il.EmitCalli(OpCodes.Calli, CallingConvention.Cdecl, typeof(void), Type.EmptyTypes);
                        type.DefinePInvokeMethod("x", "unused", MethodAttributes.Public | MethodAttributes.Static,
                            CallingConventions.Standard, typeof(void), Type.EmptyTypes, CallingConvention.Winapi, CharSet.Unicode);
                    }
                }
                """);
            var banned = await Run(directory, "build", project, "--no-restore", "-nr:false");
            Assert.NotEqual(0, banned.ExitCode);
            foreach (var symbol in new[] { "PriorityClass", "ProcessorAffinity", "HasExited", "ProcessName", "GetProcessById", "WaitForExit()", "WaitForExitAsync(", "DynamicMethod", "EmitCalli", "DefinePInvokeMethod" })
                Assert.Contains(banned.Output.Split('\n'), line => line.Contains("error RS0030:", StringComparison.Ordinal) && line.Contains(symbol, StringComparison.Ordinal));

        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IEnumerable<(string Mutation, string Expected)> Cases()
    {
        yield return ("<ItemGroup><AdditionalFiles Remove=\"" + System.Security.SecurityElement.Escape(Path.Combine(Repository.Root, "BannedSymbols.Production.txt")) + "\" /></ItemGroup>", "Missing repository BannedSymbols.Production.txt");
        yield return ("<PropertyGroup><_SkipAnalyzers>true</_SkipAnalyzers></PropertyGroup>", "_SkipAnalyzers must not");
        yield return ("<PropertyGroup><CompilerResponseFile>compiler.rsp</CompilerResponseFile></PropertyGroup>", "CompilerResponseFile is forbidden");
        yield return ("<ItemGroup><CompilerResponseFile Include=\"compiler.rsp\" /></ItemGroup>", "CompilerResponseFile is forbidden");
        yield return ("<ItemGroup><Analyzer Include=\"ExtraAnalyzer.dll\" /></ItemGroup>", "Unapproved analyzer");
        yield return ("<PropertyGroup><Hidden>RS0030</Hidden><NoWarn>$(NoWarn);$(Hidden)</NoWarn></PropertyGroup>", "NoWarn contains");
        yield return ("<PropertyGroup><WarningsNotAsErrors>RS0030</WarningsNotAsErrors></PropertyGroup>", "WarningsNotAsErrors contains");
        yield return ("<PropertyGroup><TreatWarningsAsErrors>false</TreatWarningsAsErrors><WarningsAsErrors /></PropertyGroup>", "WarningsAsErrors must contain");
        yield return ("<PropertyGroup><RunAnalyzers>false</RunAnalyzers></PropertyGroup>", "Analyzers must run");
        yield return ("<PropertyGroup><RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild></PropertyGroup>", "Analyzers must run");
        yield return ("<ItemGroup><Analyzer Remove=\"@(Analyzer)\" /></ItemGroup>", "Missing BannedApiAnalyzers");
        yield return ("<ItemGroup><AdditionalFiles Remove=\"@(AdditionalFiles)\" /></ItemGroup>", "Missing repository BannedSymbols.txt");
        yield return ("<ItemGroup><AdditionalFiles Remove=\"@(AdditionalFiles->'%(Identity)')\" /></ItemGroup>", "Missing repository BannedSymbols.txt");
        yield return ("<ItemGroup><GlobalAnalyzerConfigFiles Include=\"policy.txt\" /></ItemGroup>", "Unapproved analyzer config");
        yield return ("<ItemGroup><EditorConfigFiles Include=\"policy.txt\" /></ItemGroup>", "Unapproved analyzer config");
    }

    private static Task<(int ExitCode, string Output)> Run(string directory, params string[] arguments) =>
        DotnetProcess.Run(directory, arguments);
}
