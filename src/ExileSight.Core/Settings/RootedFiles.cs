namespace ExileSight.Core.Settings;

/// <summary>Resolve a linked root once; reject links inside it. One owner controls this tree.
/// Hostile concurrent filesystem replacement is outside this contract.</summary>
internal sealed class RootedFiles
{
    private readonly string originalRoot;
    private readonly string root;
    private readonly Action<string, string, string?> replace;

    public RootedFiles(string root, Action<string, string, string?>? replace = null)
    {
        originalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        this.root = ResolveRoot(originalRoot);
        this.replace = replace ?? ((source, destination, backup) => File.Replace(source, destination, backup));
    }

    internal static string ResolveRoot(string root) =>
        new DirectoryInfo(root).LinkTarget is not null
            ? new DirectoryInfo(root).ResolveLinkTarget(true)!.FullName : Path.GetFullPath(root);

    public string Check(string path)
    {
        path = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(originalRoot, path);
        if (Escapes(relative)) relative = Path.GetRelativePath(root, path);
        if (Escapes(relative)) throw new IOException("Settings path escapes the application root.");
        path = Path.GetFullPath(Path.Combine(root, relative));
        for (string? current = path; current is not null && current != root; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current) || new FileInfo(current).LinkTarget is not null) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Settings paths cannot contain symbolic links.");
        }
        return path;
    }

    private static bool Escapes(string relative) => Path.IsPathRooted(relative) || relative == ".." ||
        relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    public void Write(string path, byte[] bytes, bool rotateBackup = true)
    {
        path = Check(path);
        Directory.CreateDirectory(Check(Path.GetDirectoryName(path)!));
        var temporary = Check(path + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var backup = Check(path + ".bak");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            Check(path);
            if (File.Exists(path)) replace(temporary, path, rotateBackup ? backup : null);
            else File.Move(temporary, path);
        }
        catch
        {
            // Windows ReplaceFile can move the old main before failing. Restore without consuming
            // the backup; if that fails, retain the new temporary bytes for manual recovery.
            if (!File.Exists(Check(path)))
            {
                try
                {
                    if (File.Exists(Check(backup))) File.Copy(backup, path);
                    else if (File.Exists(Check(temporary))) File.Move(temporary, path);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
            throw;
        }
        finally
        {
            if (File.Exists(Check(path)) && File.Exists(temporary)) File.Delete(Check(temporary));
        }
    }

    public void PreserveVersion(string source, string main, int version)
    {
        var backup = Check(main + ".v" + version + ".bak");
        if (!File.Exists(backup)) File.Copy(Check(source), backup, false);
    }

    public void CleanTemporaryFiles(string path)
    {
        var directory = Check(Path.GetDirectoryName(path)!);
        if (!Directory.Exists(directory)) return;
        var prefix = Path.GetFileName(path) + ".";
        foreach (var temporary in Directory.EnumerateFiles(directory, prefix + "*.tmp"))
        {
            var suffix = Path.GetFileName(temporary)[prefix.Length..^4];
            if (!Guid.TryParseExact(suffix, "N", out _)) continue;
            if (File.Exists(Check(path))) File.Delete(Check(temporary));
            else File.Move(Check(temporary), Check(temporary + ".recovery"));
        }
    }

    public void Quarantine(string path) => File.Move(Check(path),
        Check(path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N")));
}
