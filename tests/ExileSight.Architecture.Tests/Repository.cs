namespace ExileSight.Architecture.Tests;

internal static class Repository
{
    internal static readonly string Root = FindRoot();

    internal static IEnumerable<string> Files(string pattern) =>
        Directory.EnumerateFiles(Root, pattern, SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(Root, p).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "obj" or "bin" or ".git"));

    internal static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "ExileSight.slnx")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Cannot locate ExileSight.slnx above test output.");
    }
}
