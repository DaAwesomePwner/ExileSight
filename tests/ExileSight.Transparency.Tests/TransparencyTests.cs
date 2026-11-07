using Xunit;

namespace ExileSight.Transparency.Tests;

public sealed class TransparencyTests
{
    [Fact]
    public void ReadmeMatchesCurrentFeatureDeclarations()
    {
        var root = FindRoot();
        var scratch = Path.Combine(Path.GetTempPath(), "ExileSight-transparency-" + Guid.NewGuid().ToString("N"));
        try
        {
            var host = FeatureInventory.Read(root, scratch);
            var expected = TransparencyTable.Generate(host, root);
            var path = Path.Combine(root, "README.md");
            var original = File.ReadAllText(path);
            var readme = original.Replace("\r\n", "\n");
            var start = readme.IndexOf(TransparencyTable.Start, StringComparison.Ordinal);
            var end = readme.IndexOf(TransparencyTable.End, StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start, "README must contain ordered transparency:start/end markers.");
            Assert.Equal(start, readme.LastIndexOf(TransparencyTable.Start, StringComparison.Ordinal));
            Assert.Equal(end, readme.LastIndexOf(TransparencyTable.End, StringComparison.Ordinal));
            end += TransparencyTable.End.Length;
            if (Environment.GetEnvironmentVariable("UPDATE_TRANSPARENCY") == "1")
            {
                var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
                File.WriteAllText(path, (readme[..start] + expected + readme[end..]).Replace("\n", newline));
                readme = File.ReadAllText(path).Replace("\r\n", "\n");
                end = readme.IndexOf(TransparencyTable.End, StringComparison.Ordinal) + TransparencyTable.End.Length;
            }
            var actual = readme[start..end];
            Assert.True(actual == expected, Difference(actual, expected));
            Assert.False(Directory.Exists(scratch), "Reading feature declarations unexpectedly wrote to storage.");
        }
        finally { if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true); }
    }

    private static string Difference(string actual, string expected)
    {
        var oldLines = actual.Split('\n'); var newLines = expected.Split('\n');
        var changes = Enumerable.Range(0, Math.Max(oldLines.Length, newLines.Length))
            .Where(i => oldLines.ElementAtOrDefault(i) != newLines.ElementAtOrDefault(i))
            .Select(i => $"Line {i + 1}:\n- {oldLines.ElementAtOrDefault(i) ?? "<missing>"}\n+ {newLines.ElementAtOrDefault(i) ?? "<missing>"}");
        return $"README transparency section is stale. Regenerate: {TransparencyTable.UpdateCommand}\n" + string.Join('\n', changes);
    }

    internal static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ExileSight.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot locate ExileSight.slnx above test output.");
    }
}
