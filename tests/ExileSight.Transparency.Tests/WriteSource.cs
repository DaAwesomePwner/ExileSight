using System.Text.RegularExpressions;

namespace ExileSight.Transparency.Tests;

// Conservative source tripwire: every hit requires a nonempty supplemental write declaration.
// This is not dataflow analysis or an exact path audit. Extend the patterns when adding a shared
// persistence helper. Save/Write calls through store/files and context.WriteSetting cover current
// atomic-store wrappers; primitives catch new implementations. Comments may intentionally trigger it.
internal static class WriteSource
{
    private const string Pattern =
        @"\bFile\s*\.\s*(?:Write\w*|Append\w*|Move|Replace|Create\w*|Copy|Delete|OpenWrite)\b|" +
        @"\bDirectory\s*\.\s*CreateDirectory\s*\(|" +
        @"\bFileStream\s*\([^;]*?\b(?:FileMode\s*\.\s*(?:Create|CreateNew|OpenOrCreate|Append|Truncate)|FileAccess\s*\.\s*(?:Write|ReadWrite))\b|" +
        @"\bWriteAtomic\s*\(|\bWriteSetting\s*\(|" +
        @"\b(?:store|files)\s*\??\s*\.\s*(?:Save|Write|WriteAsync|WriteCustom|WriteActive|Quarantine|PreserveVersion)\s*\(";

    internal static bool HasWrite(string source) => Regex.IsMatch(source, Pattern, RegexOptions.Singleline);

    internal static void AssertDeclared(string id, string file, string source, IReadOnlyDictionary<string, string[]> declarations)
    {
        if (HasWrite(source) && (!declarations.TryGetValue(id, out var writes) || writes.Length == 0))
            throw new InvalidOperationException($"Feature {id} has a write API in {file} but declares no writes. " +
                "Audit its paths and update DeclaredResources.FeatureWrites before regenerating README.");
    }
}
