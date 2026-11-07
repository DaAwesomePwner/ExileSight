using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class SourceBoundaryTests
{
    [Fact]
    public void OnlyActionGateMaySuppressBannedDiagnostic()
    {
        foreach (var path in Repository.Files("*").Where(BoundaryScanner.IsScanned))
        {
            if (Repository.Relative(path).Equals("src/ExileSight.Platform.Windows/Input/ActionGate.cs", StringComparison.OrdinalIgnoreCase))
                continue;
            var text = File.ReadAllText(path);
            if (Repository.Relative(path) == "tests/ExileSight.Architecture.Tests/DotnetProcess.cs")
            {
                const string exception = "#pragma warning disable RS0030 // Test-only process owner: terminate only the child tree started above.";
                Assert.Equal(2, text.Split(exception, StringSplitOptions.None).Length);
                text = text.Replace(exception, "", StringComparison.Ordinal);
            }
            Assert.False(BoundaryScanner.Suppression(text, Path.GetExtension(path)), path);
        }
    }

    [Fact]
    public void BuildControlTokensCannotOverrideTheGuard()
    {
        foreach (var path in Repository.Files("*").Where(p => new[] { ".csproj", ".props", ".targets", ".rsp" }
                     .Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase)))
        {
            if (Repository.Relative(path) == "Directory.Build.targets")
                continue;
            Assert.False(BoundaryScanner.GuardOverride(File.ReadAllText(path)), path);
        }
    }

    [Fact]
    public void HandWrittenCodeHasNoInteropOrUnsafeHooks()
    {
        foreach (var path in Repository.Files("*.cs").Where(Repository.IsProduction))
        {
            Assert.False(BoundaryScanner.Interop(File.ReadAllText(path)), path);
            Assert.False(BoundaryScanner.UnsafeHooks(File.ReadAllText(path)), path);
        }
    }
}
