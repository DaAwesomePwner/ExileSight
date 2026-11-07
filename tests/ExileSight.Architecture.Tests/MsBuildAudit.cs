using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ExileSight.Architecture.Tests;

internal static class MsBuildAudit
{
    private const string Properties = "_SkipAnalyzers,NoWarn,WarningsNotAsErrors,WarningsAsErrors,TreatWarningsAsErrors,RunAnalyzers,RunAnalyzersDuringBuild,CompilerResponseFile,ImportDirectoryBuildTargets,ImportDirectoryBuildProps,DirectoryBuildTargetsPath,DirectoryBuildPropsPath,ExileSightGuardBeforeRan,ExileSightGuardAfterRan,MSBuildToolsPath,NuGetPackageRoot,NetCoreTargetingPackRoot,GeneratedMSBuildEditorConfigFile";

    internal static async Task Check(string project, params string[] extraArguments)
    {
        var directory = Path.GetDirectoryName(project)!;
        var args = new List<string>
        {
            "msbuild", project, "-t:Compile", "-p:SkipCompilerExecution=true", "-p:BuildProjectReferences=false", "-nr:false",
            "-p:Configuration=" + new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,
            "-getProperty:" + Properties,
            "-getItem:Analyzer,AdditionalFiles,EditorConfigFiles,GlobalAnalyzerConfigFiles,CompilerResponseFile",
            "-getTargetResult:ExileSightGuardBeforeCompile,ExileSightGuardAfterCompile"
        };
        args.AddRange(extraArguments);
        var result = await DotnetProcess.Run(directory, args);
        Assert.True(result.ExitCode == 0, "Independent MSBuild audit failed: " + project + "\n" + result.Output);
        using var json = JsonDocument.Parse(result.Output[result.Output.IndexOf('{')..]);
        Validate(json.RootElement, directory);
    }

    internal static void Validate(JsonElement state, string directory)
    {
        var properties = state.GetProperty("Properties");
        string Value(string name) => properties.GetProperty(name).GetString() ?? "";
        string Full(string path) => Path.GetFullPath(path, directory);
        static bool IsTrue(string value) => value.Equals("true", StringComparison.OrdinalIgnoreCase);
        static bool IsFalse(string value) => value.Equals("false", StringComparison.OrdinalIgnoreCase);
        static bool ContainsBan(string value) => Regex.IsMatch(value, @"\bRS0030\b", RegexOptions.IgnoreCase);
        string[] Items(string name) => state.GetProperty("Items").GetProperty(name).EnumerateArray()
            .Select(item => item.GetProperty("FullPath").GetString()!).ToArray();

        Assert.False(IsTrue(Value("_SkipAnalyzers")), "_SkipAnalyzers disables compiler analysis.");
        Assert.False(ContainsBan(Value("NoWarn")), "Evaluated NoWarn suppresses RS0030.");
        Assert.False(ContainsBan(Value("WarningsNotAsErrors")), "Evaluated WarningsNotAsErrors downgrades RS0030.");
        Assert.True(IsTrue(Value("TreatWarningsAsErrors")) || ContainsBan(Value("WarningsAsErrors")));
        Assert.False(IsFalse(Value("RunAnalyzers")) || IsFalse(Value("RunAnalyzersDuringBuild")));
        Assert.True(string.IsNullOrWhiteSpace(Value("CompilerResponseFile")));
        Assert.Empty(Items("CompilerResponseFile"));
        Assert.True(IsTrue(Value("ImportDirectoryBuildProps")) && IsTrue(Value("ImportDirectoryBuildTargets")));
        Assert.Equal(Path.Combine(Repository.Root, "Directory.Build.props"), Full(Value("DirectoryBuildPropsPath")), ignoreCase: true);
        Assert.Equal(Path.Combine(Repository.Root, "Directory.Build.targets"), Full(Value("DirectoryBuildTargetsPath")), ignoreCase: true);
        foreach (var (target, marker) in new[] { ("ExileSightGuardBeforeCompile", "ExileSightGuardBeforeRan"), ("ExileSightGuardAfterCompile", "ExileSightGuardAfterRan") })
        {
            Assert.Equal("Success", state.GetProperty("TargetResults").GetProperty(target).GetProperty("Result").GetString());
            Assert.True(IsTrue(Value(marker)), target + " did not execute its real guard body.");
        }
        var analyzers = Items("Analyzer");
        var allowed = File.ReadAllLines(Path.Combine(Repository.Root, "tests/ExileSight.Architecture.Tests/AnalyzerAllowlist.txt"))
            .Where(line => !string.IsNullOrWhiteSpace(line)).Select(line =>
            {
                var split = line.IndexOf('/');
                var root = line[..split] switch { "sdk" => Value("MSBuildToolsPath"), "packs" => Value("NetCoreTargetingPackRoot"), _ => Value("NuGetPackageRoot") };
                return Path.GetFullPath(Path.Combine(root, line[(split + 1)..]));
            }).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var analyzer in analyzers)
            Assert.True(allowed.Contains(analyzer) && File.Exists(analyzer), "Unapproved analyzer: " + analyzer);
        foreach (var name in new[] { "Microsoft.CodeAnalysis.BannedApiAnalyzers.dll", "Microsoft.CodeAnalysis.CSharp.BannedApiAnalyzers.dll" })
            Assert.Contains(analyzers, path => Path.GetFileName(path) == name);
        Assert.Contains(Items("AdditionalFiles"), path => path.Equals(Path.Combine(Repository.Root, "BannedSymbols.txt"), StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(Path.Combine(Repository.Root, "BannedSymbols.txt")));
        var productionBans = Path.Combine(Repository.Root, "BannedSymbols.Production.txt");
        var hasProductionBans = Items("AdditionalFiles").Contains(productionBans, StringComparer.OrdinalIgnoreCase);
        if (Repository.IsProduction(Path.Combine(directory, "project.csproj")))
            Assert.True(File.Exists(productionBans) && hasProductionBans, "Missing production ban compiler input.");
        else
            Assert.False(hasProductionBans, "Production Process bans must not apply to test projects.");
        foreach (var config in Items("EditorConfigFiles").Concat(Items("GlobalAnalyzerConfigFiles")).Where(File.Exists))
        {
            var generated = config.Equals(Full(Value("GeneratedMSBuildEditorConfigFile")), StringComparison.OrdinalIgnoreCase);
            var sdk = new[] { "analyzers/build/config", "codestyle/cs/build/config" }.Any(folder => config.StartsWith(
                Path.GetFullPath(Path.Combine(Value("MSBuildToolsPath"), "Sdks/Microsoft.NET.Sdk", folder)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            Assert.True(config.Equals(Path.Combine(Repository.Root, ".editorconfig"), StringComparison.OrdinalIgnoreCase) || generated || sdk,
                "Unapproved analyzer config: " + config);
            if (generated)
                Assert.DoesNotMatch(@"(?i)RS0030|ApiDesign|dotnet_analyzer_diagnostic", File.ReadAllText(config));
        }
    }
}
