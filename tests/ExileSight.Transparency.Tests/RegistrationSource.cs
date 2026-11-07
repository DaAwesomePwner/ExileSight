using System.Text.RegularExpressions;

namespace ExileSight.Transparency.Tests;

// A deliberately narrow source guard, not a C# interpreter. Unknown composition syntax fails closed;
// extend it when App changes its registration convention. It never loads the Windows/WPF App.
internal static class RegistrationSource
{
    internal static string[] Read(string root, IReadOnlyList<Type> types)
    {
        var directory = Path.Combine(root, "src/ExileSight.App/Features");
        var source = File.ReadAllText(Path.Combine(directory, "FeatureRegistration.cs"));
        var match = Regex.Match(source, @"CompiledModules\([^)]*\)\s*=>\s*\[(.*?)\];", RegexOptions.Singleline);
        if (!match.Success) throw new InvalidOperationException("Cannot parse App CompiledModules; update RegistrationSource.");
        return Entries(match.Groups[1].Value).Select(entry => Resolve(entry, directory, types)).ToArray();
    }

    private static string Resolve(string entry, string directory, IReadOnlyList<Type> types)
    {
        var direct = Regex.Match(entry, @"^new\s+(ExileSight\.Features\.\w+\.\w+)\b");
        if (direct.Success) return direct.Groups[1].Value;
        var factory = Regex.Match(entry, @"^(\w+)\.(\w+Registration)\.Create\(services\)$");
        if (!factory.Success) throw new InvalidOperationException("Unknown App feature registration: " + entry + ". Update RegistrationSource.");
        var source = File.ReadAllText(Path.Combine(directory, factory.Groups[1].Value, factory.Groups[2].Value + ".cs"));
        var method = Regex.Match(source, @"\bCreate\(IServiceProvider\s+services\)(.*?)(?=\b(?:public|private|internal)\s+static\b|\z)", RegexOptions.Singleline);
        var constructors = Regex.Matches(method.Groups[1].Value, @"\bnew\s+([\w.]+Feature)\b")
            .Select(m => m.Groups[1].Value).Distinct().ToArray();
        if (constructors.Length != 1)
            throw new InvalidOperationException("Cannot resolve feature returned by " + factory.Value + "; update RegistrationSource.");
        var name = constructors[0];
        var type = types.SingleOrDefault(t => t.FullName == name || t.Name == name &&
            source.Contains("using " + t.Namespace + ";", StringComparison.Ordinal));
        return type?.FullName ?? throw new InvalidOperationException("App registers unknown feature " + name + "; add its project reference.");
    }

    private static IEnumerable<string> Entries(string source)
    {
        // Hide comments and ordinary strings before counting C# collection/initializer delimiters.
        source = Regex.Replace(source, "//[^\r\n]*|/\\*.*?\\*/|\"(?:\\\\.|[^\"\\\\])*\"", "\"\"", RegexOptions.Singleline);
        var start = 0; var depth = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] is '(' or '[' or '{') depth++;
            if (source[i] is ')' or ']' or '}') depth--;
            if (source[i] != ',' || depth != 0) continue;
            yield return source[start..i].Trim(); start = i + 1;
        }
        var last = source[start..].Trim();
        if (last.Length > 0) yield return last;
    }
}
