using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Platform.Windows.Input;

internal sealed class FileActionAudit : IActionAudit
{
    public string Path { get; } = ExileSight.Core.Input.ActionLogPath.Default;
    private FileStream Open()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        return new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read);
    }
    public void EnsureWritable() { using var stream = Open(); }
    public void Append(string line)
    {
        using var stream = Open();
        using var writer = new StreamWriter(stream);
        writer.WriteLine(line);
        writer.Flush();
    }
}
