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
