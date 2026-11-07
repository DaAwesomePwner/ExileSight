using System.Text.RegularExpressions;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class SourceBoundaryTests
{
    [Fact]
    public void OnlyActionGateMaySuppressBannedDiagnostic()
    {
        foreach (var path in Repository.Files("*").Where(p =>
                     Path.GetExtension(p) is ".cs" or ".props" or ".targets" or ".csproj" or ".editorconfig" or ".globalconfig"
                     || Path.GetFileName(p) is ".editorconfig" or ".globalconfig"))
        {
            if (Repository.Relative(path) == "src/ExileSight.Platform.Windows/Input/ActionGate.cs")
                continue;
            var text = File.ReadAllText(path);
            // Construct the identifier so the test's own literals do not look like suppressions.
            var diagnostic = "RS" + "0030";
            Assert.False(Regex.IsMatch(text, @"(?m)^\s*#pragma\s+warning\s+disable(?:[ \t]*(?://[^\r\n]*)?$|[^\r\n]*\b" + diagnostic + @"\b)"), path);
            Assert.False(Regex.IsMatch(text, @"SuppressMessage\s*\([^)]*" + diagnostic, RegexOptions.Singleline), path);
            Assert.False(Regex.IsMatch(text, @"<NoWarn>[^<]*" + diagnostic, RegexOptions.IgnoreCase), path);
            Assert.False(Regex.IsMatch(text, @"dotnet_diagnostic\." + diagnostic + @"\.severity\s*=\s*(none|silent|suggestion)", RegexOptions.IgnoreCase), path);
            Assert.False(Regex.IsMatch(text, @"<EnableNETAnalyzers>\s*false|<RunAnalyzers[^>]*>\s*false", RegexOptions.IgnoreCase), path);
        }
    }

    [Fact]
    public void NativeImportsStayInWindowsPlatform()
    {
        foreach (var path in Repository.Files("*.cs").Where(p => Repository.Relative(p).StartsWith("src/", StringComparison.Ordinal)))
        {
            if (Repository.Relative(path).StartsWith("src/ExileSight.Platform.Windows/", StringComparison.Ordinal))
                continue;
            Assert.DoesNotMatch(@"\b(?:Dll" + @"Import|Library" + @"Import)(?:Attribute)?\b", File.ReadAllText(path));
        }
    }

    [Fact]
    public void HooksUseOnlyLiteralLowLevelHookIds()
    {
        foreach (var path in Repository.Files("*.cs").Where(p => Repository.Relative(p).StartsWith("src/", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(path);
            // Fail closed: aliases, numeric ids and variables require review, not an exemption.
            foreach (Match match in Regex.Matches(text, @"\bSetWindowsHookEx(?:A|W)?\s*\(\s*([^,]+),"))
                Assert.Matches(@"^(?:idHook\s*:\s*)?(?:[\w]+\.)*WH_(?:KEYBOARD|MOUSE)_LL\s*$", match.Groups[1].Value);
        }
    }
}
