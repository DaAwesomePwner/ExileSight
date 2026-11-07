using System.Text.Json;
using System.Xml.Linq;
using Xunit;
using Xunit.Sdk;

namespace ExileSight.Architecture.Tests;

public sealed class MsBuildAuditTests
{
    [Fact]
    public async Task EverySolutionProjectHasAnIndependentEvaluatedAudit()
    {
        var projects = XDocument.Load(Path.Combine(Repository.Root, "ExileSight.slnx")).Descendants("Project")
            .Select(element => Path.Combine(Repository.Root, element.Attribute("Path")!.Value)).ToArray();
        Assert.NotEmpty(projects);
        using var limit = new SemaphoreSlim(3);
        await Task.WhenAll(projects.Select(async project =>
        {
            await limit.WaitAsync();
            try { await MsBuildAudit.Check(project); }
            finally { limit.Release(); }
        }));
    }

    [Fact]
    public async Task ImportBypassesAndEmptyGuardRedefinitionsFailIndependentAudit()
    {
        var directory = Path.Combine(Repository.Root, "tests/ExileSight.Architecture.Tests/obj", "audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var project = Path.Combine(directory, "AuditProbe.csproj");
            await File.WriteAllTextAsync(project, """
                <Project>
                  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework><NuGetAudit>false</NuGetAudit></PropertyGroup>
                  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
                  <Import Project="Override.targets" />
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(directory, "Probe.cs"), "namespace AuditProbe; public class Probe { }");
            var overrides = Path.Combine(directory, "Override.targets");
            var empty = Path.Combine(directory, "Empty.targets");
            await File.WriteAllTextAsync(overrides, "<Project />");
            await File.WriteAllTextAsync(empty, "<Project />");
            using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository.Root, "tests/ExileSight.Architecture.Tests/obj/project.assets.json")));
            var cache = assets.RootElement.GetProperty("packageFolders").EnumerateObject().First().Name;
            var restore = await DotnetProcess.Run(directory, ["restore", project, "--source", cache, "-p:NuGetAudit=false", "-nr:false"]);
            Assert.True(restore.ExitCode == 0, restore.Output);
            await MsBuildAudit.Check(project);
            foreach (var property in new[] { "ImportDirectoryBuildTargets=false", "ImportDirectoryBuildProps=false", "DirectoryBuildTargetsPath=" + empty, "DirectoryBuildPropsPath=" + empty })
                await Assert.ThrowsAnyAsync<XunitException>(() => MsBuildAudit.Check(project, "-p:" + property));
            foreach (var target in new[] { "ExileSightGuardBeforeCompile", "ExileSightGuardAfterCompile" })
            {
                await File.WriteAllTextAsync(overrides, "<Project><Target Name=\"" + target + "\" /></Project>");
                await Assert.ThrowsAnyAsync<XunitException>(() => MsBuildAudit.Check(project));
            }
            // Even spoofed success markers cannot bypass independent final-value checks.
            await File.WriteAllTextAsync(overrides, """
                <Project>
                  <Target Name="ExileSightGuardBeforeCompile" BeforeTargets="CoreCompile">
                    <PropertyGroup><ExileSightGuardBeforeRan>true</ExileSightGuardBeforeRan></PropertyGroup>
                  </Target>
                  <Target Name="ExileSightGuardAfterCompile" AfterTargets="CoreCompile">
                    <PropertyGroup><ExileSightGuardAfterRan>true</ExileSightGuardAfterRan><Hidden>RS0030</Hidden><NoWarn>$(Hidden)</NoWarn></PropertyGroup>
                  </Target>
                </Project>
                """);
            await Assert.ThrowsAnyAsync<XunitException>(() => MsBuildAudit.Check(project));
            File.Copy(typeof(MsBuildAuditTests).Assembly.Location, Path.Combine(directory, "ExtraAnalyzer.dll"));
            await File.WriteAllTextAsync(overrides, """
                <Project>
                  <Target Name="ExileSightGuardBeforeCompile" BeforeTargets="CoreCompile">
                    <PropertyGroup><ExileSightGuardBeforeRan>true</ExileSightGuardBeforeRan></PropertyGroup>
                  </Target>
                  <Target Name="ExileSightGuardAfterCompile" AfterTargets="CoreCompile">
                    <PropertyGroup><ExileSightGuardAfterRan>true</ExileSightGuardAfterRan></PropertyGroup>
                    <ItemGroup><Analyzer Include="ExtraAnalyzer.dll" /></ItemGroup>
                  </Target>
                </Project>
                """);
            var rejected = await Assert.ThrowsAnyAsync<XunitException>(() => MsBuildAudit.Check(project));
            Assert.Contains("Unapproved analyzer", rejected.Message);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
