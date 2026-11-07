using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ExileSight.Architecture.Tests;

internal static class BoundaryScanner
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Multiline;
    internal static bool IsScanned(string path) => new[]
    {
        ".cs", ".csproj", ".props", ".targets", ".rsp", ".ruleset", ".editorconfig", ".globalconfig"
    }.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    // Preserve newlines for directives, remove comments and literal contents before C# token scans.
    internal static string Code(string text) => Regex.Replace(text,
        "//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'",
        match => Regex.Replace(match.Value, @"[^\r\n]", " "));

    internal static bool Suppression(string text, string extension)
    {
        if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            var code = Code(text);
            return Regex.IsMatch(code, @"^\s*#pragma\s+warning\s+disable(?:[ \t]*\r?$|[^\r\n]*\bRS0030\b)", Options)
                || Regex.IsMatch(code, @"\b(?:UnconditionalSuppressMessage|SuppressMessage)(?:Attribute)?\s*\(");
        }
        return XmlSuppression(text) || Regex.IsMatch(text, @"<(?:NoWarn|WarningsNotAsErrors)\b[^>]*>[^<]*RS0030", Options)
            || Regex.IsMatch(text, @"<(?:CodeAnalysisTreatWarningsAsErrors|RunAnalyzers|RunAnalyzersDuringBuild|EnableNETAnalyzers)\b[^>]*>\s*false", Options)
            || Regex.IsMatch(text, @"dotnet_(?:diagnostic\.RS0030|analyzer_diagnostic(?:\.category-ApiDesign)?)\.severity\s*=\s*(?!error\b)\w+", Options)
            || Regex.IsMatch(text, @"<PackageReference\b[^>]*\b(?:Remove|Update)\s*=\s*['"" ][^>]*BannedApiAnalyzers", Options)
            || Regex.IsMatch(text, @"<AdditionalFiles\b[^>]*\bRemove\s*=\s*['""][^>]*(?:BannedSymbols|\*)", Options)
            || Regex.IsMatch(text, @"(?:/|-)(?:nowarn|warnaserror-)\s*:[^\r\n]*RS0030", Options)
            || Regex.IsMatch(text, @"<Rule\b(?=[^>]*\bId\s*=\s*['""]RS0030['""])(?=[^>]*\bAction\s*=\s*['""](?!Error)[^'""]+['""])", Options);
    }

    private static bool XmlSuppression(string text)
    {
        try
        {
            var xml = XDocument.Parse(text);
            foreach (var element in xml.Descendants())
            {
                var name = element.Name.LocalName;
                if ((name is "NoWarn" or "WarningsNotAsErrors") && element.Value.Contains("RS0030", StringComparison.OrdinalIgnoreCase))
                    return true;
                if ((name is "CodeAnalysisTreatWarningsAsErrors" or "RunAnalyzers" or "RunAnalyzersDuringBuild" or "EnableNETAnalyzers")
                    && element.Value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (name == "PackageReference" && element.Attributes().Any(a => (a.Name.LocalName is "Remove" or "Update")
                    && (a.Value.Contains("BannedApiAnalyzers", StringComparison.OrdinalIgnoreCase) || a.Value.Contains('*'))))
                    return true;
                if (name == "AdditionalFiles" && element.Attribute("Remove") is { } remove
                    && (remove.Value.Contains("BannedSymbols", StringComparison.OrdinalIgnoreCase) || remove.Value.Contains('*')))
                    return true;
                if (name == "Rule" && (string?)element.Attribute("Id") == "RS0030" && (string?)element.Attribute("Action") != "Error")
                    return true;
            }
        }
        catch (System.Xml.XmlException)
        {
            // Non-XML settings and incomplete snippets are checked lexically below.
        }
        return false;
    }

    internal static bool Interop(string text) => Regex.IsMatch(Code(text),
        @"\b(?:DllImport|LibraryImport)(?:Attribute)?\b|\bextern\b|\bdelegate\s*\*\s*unmanaged\b");

    internal static bool UnsafeHooks(string text)
    {
        var code = Code(text);
        foreach (Match reference in Regex.Matches(code, @"\bSetWindowsHookEx(?:A|W)?\b"))
        {
            var tail = code[(reference.Index + reference.Length)..];
            if (!Regex.IsMatch(tail, @"^\s*\(\s*(?:idHook\s*:\s*)?(?:\w+\.)*WH_(?:KEYBOARD|MOUSE)_LL\s*,"))
                return true;
        }
        foreach (Match reference in Regex.Matches(code, @"\bSetWinEventHook\b"))
        {
            var tail = code[(reference.Index + reference.Length)..];
            // Deliberately fail closed on indirect/nested expressions and method groups.
            var call = Regex.Match(tail, @"^\s*\(([^()]*)\)");
            if (!call.Success)
                return true;
            var args = call.Groups[1].Value.Split(',');
            if (args.Length != 7 || !Regex.IsMatch(args[6], @"\bWINEVENT_OUTOFCONTEXT\b")
                || !Regex.IsMatch(args[6], @"^\s*(?:dwFlags\s*:\s*)?(?:\w+\.)*WINEVENT_(?:OUTOFCONTEXT|SKIPOWNPROCESS|SKIPOWNTHREAD)(?:\s*\|\s*(?:\w+\.)*WINEVENT_(?:OUTOFCONTEXT|SKIPOWNPROCESS|SKIPOWNTHREAD))*\s*$"))
                return true;
        }
        return false;
    }
}
