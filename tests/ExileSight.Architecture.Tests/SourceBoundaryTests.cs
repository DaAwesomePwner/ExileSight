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
            Assert.False(BoundaryScanner.Suppression(File.ReadAllText(path), Path.GetExtension(path)), path);
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
