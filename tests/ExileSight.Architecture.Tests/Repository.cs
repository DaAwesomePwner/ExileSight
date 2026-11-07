namespace ExileSight.Architecture.Tests;

internal static class Repository
{
    internal static readonly string Root = FindRoot();

    internal static IEnumerable<string> Files(string pattern) => Walk(Root, pattern);

    private static IEnumerable<string> Walk(string directory, string pattern)
    {
        foreach (var file in Directory.EnumerateFiles(directory, pattern))
            yield return file;
        foreach (var child in Directory.EnumerateDirectories(directory).Where(p =>
                     !new[] { "obj", "bin", ".git" }.Contains(Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)))
            foreach (var file in Walk(child, pattern))
                yield return file;
    }

    internal static bool IsProduction(string path) => new[] { "src/", "tools/" }
        .Any(prefix => Relative(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    internal static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "ExileSight.slnx")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Cannot locate ExileSight.slnx above test output.");
    }
}
