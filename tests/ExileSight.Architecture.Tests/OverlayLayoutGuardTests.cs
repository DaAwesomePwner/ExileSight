using System.Text.RegularExpressions;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class OverlayLayoutGuardTests
{
    [Fact]
    public void AppUsesCentralOverlayLayout()
    {
        // Fixed rectangles belong in Core/Overlay/DefaultOverlayLayout.cs.
        // No popup exceptions currently exist. Any future cursor-popup exception must
        // be scoped to its exact construction and prove coordinates derive from the cursor.
        var offenders = Repository.Files("*.cs")
            .Where(path => Repository.Relative(path).StartsWith("src/ExileSight.App/", StringComparison.Ordinal))
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"new\s+OverlayRect\s*\("))
            .Select(Repository.Relative).ToArray();
        Assert.Empty(offenders);
    }
}
