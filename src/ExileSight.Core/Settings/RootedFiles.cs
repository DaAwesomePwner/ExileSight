namespace ExileSight.Core.Settings;

/// <summary>Reject escapes and existing symlinks, at or below the configured root.
/// The application owns this tree; hostile concurrent filesystem replacement is outside this contract.</summary>
internal sealed class RootedFiles(string root)
{
    private readonly string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    public string Check(string path)
    {
        path = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new IOException("Settings path escapes the application root.");
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            {
            if ((File.Exists(current) || Directory.Exists(current) || new FileInfo(current).LinkTarget is not null) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Settings paths cannot contain symbolic links.");
            if (current == root) break;
        }
        return path;
    }

    public void Write(string path, byte[] bytes)
    {
        Check(path);
        Directory.CreateDirectory(Check(Path.GetDirectoryName(path)!));
        var temporary = Check(path + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            Check(path);
            if (File.Exists(path))
                File.Replace(temporary, path, Check(path + ".bak"));
            else
                File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(Check(temporary));
        }
    }

    public void Quarantine(string path) => File.Move(Check(path),
        Check(path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N")));
}
