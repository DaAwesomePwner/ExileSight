using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class BoundaryScannerTests
{
    [Theory]
    [InlineData("#pragma warning disable\r\n", ".cs")]
    [InlineData("#pragma warning disable // comment\r\n", ".cs")]
    [InlineData("#pragma warning disable RS0030 // comment\r\n", ".cs")]
    [InlineData("[SuppressMessage(\"Api)Design\", \"RS\" + \"0030\")]", ".cs")]
    [InlineData("[UnconditionalSuppressMessage(\"ApiDesign\", \"RS0030\")]", ".cs")]
    [InlineData("<NoWarn Condition=\"true\">$(NoWarn);RS0030</NoWarn>", ".props")]
    [InlineData("<WarningsNotAsErrors>RS0030</WarningsNotAsErrors>", ".targets")]
    [InlineData("dotnet_diagnostic.RS0030.severity = none", ".editorconfig")]
    [InlineData("dotnet_analyzer_diagnostic.category-ApiDesign.severity = warning", ".globalconfig")]
    [InlineData("dotnet_analyzer_diagnostic.severity = silent", ".globalconfig")]
    [InlineData("<PackageReference Remove=\"Microsoft.CodeAnalysis.BannedApiAnalyzers\" />", ".csproj")]
    [InlineData("<PackageReference Update=\"Microsoft.CodeAnalysis.BannedApiAnalyzers\" />", ".targets")]
    [InlineData("<AdditionalFiles Remove=\"../BannedSymbols.txt\" />", ".props")]
    [InlineData("<CodeAnalysisTreatWarningsAsErrors>false</CodeAnalysisTreatWarningsAsErrors>", ".props")]
    [InlineData("<RunAnalyzers>false</RunAnalyzers>", ".targets")]
    [InlineData("<RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>", ".targets")]
    [InlineData("<EnableNETAnalyzers>false</EnableNETAnalyzers>", ".props")]
    [InlineData("/nowarn:RS0030", ".rsp")]
    [InlineData("<Rule Id=\"RS0030\" Action=\"None\" />", ".ruleset")]
    [InlineData("<NoWarn Condition=\"1 > 0\">$(NoWarn);RS0030</NoWarn>", ".props")]
    public void DetectsSuppression(string text, string extension) => Assert.True(BoundaryScanner.Suppression(text, extension));

    [Theory]
    [InlineData("Directory.Build.targets")]
    [InlineData("compile.rsp")]
    [InlineData("analyzers.ruleset")]
    [InlineData(".globalconfig")]
    public void IncludesConfigurationFiles(string path) => Assert.True(BoundaryScanner.IsScanned(path));

    [Theory]
    [InlineData("[DllImport(\"user32\")] static extern void Call();")]
    [InlineData("[LibraryImportAttribute(\"user32\")] static partial void Call();")]
    [InlineData("static extern void Call();")]
    [InlineData("delegate* unmanaged[Cdecl]<int> pointer;")]
    public void DetectsInterop(string text) => Assert.True(BoundaryScanner.Interop(text));

    [Theory]
    [InlineData("var hook = PInvoke.SetWindowsHookEx;")]
    [InlineData("SetWindowsHookEx(WH_KEYBOARD, callback, module, 0);")]
    [InlineData("SetWinEventHook(1, 2, module, callback, 0, 0, WINEVENT_INCONTEXT);")]
    [InlineData("SetWinEventHook(1, 2, module, callback, 0, 0, flags);")]
    [InlineData("var hook = SetWinEventHook;")]
    [InlineData("SetWinEventHook(1, 2, module, callback, 0, 0, WINEVENT_OUTOFCONTEXT | flags);")]
    public void DetectsUnsafeHooks(string text) => Assert.True(BoundaryScanner.UnsafeHooks(text));

    [Theory]
    [InlineData("SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, callback, module, 0);")]
    [InlineData("SetWindowsHookExW(WH_MOUSE_LL, callback, module, 0);")]
    [InlineData("SetWinEventHook(1, 2, module, callback, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);")]
    public void AcceptsSafeHooks(string text) => Assert.False(BoundaryScanner.UnsafeHooks(text));
}
