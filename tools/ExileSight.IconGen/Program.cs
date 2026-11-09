namespace ExileSight.IconGen;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 1 || (args.Length == 1 && args[0] != "--check"))
        {
            Console.Error.WriteLine("Usage: dotnet run --project tools/ExileSight.IconGen -- [--check]");
            return 2;
        }
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ExileSight.slnx"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Cannot locate the repository root.");
        var path = Path.Combine(root.FullName, "src/ExileSight.App/Assets/ExileSight.ico");
        var bytes = IconRenderer.Generate();
        if (args.Length == 1)
        {
            var matches = File.Exists(path) && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path));
            Console.WriteLine(matches ? "Icon matches generated geometry." : "Icon is missing or stale; regenerate it.");
            return matches ? 0 : 1;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        Console.WriteLine(path);
        return 0;
    }
}
