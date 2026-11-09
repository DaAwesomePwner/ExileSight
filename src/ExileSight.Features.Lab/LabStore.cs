namespace ExileSight.Features.Lab;

public sealed class LabStore(string directory)
{
    private string PathFor(string difficulty)
    {
        if (!LabImport.Difficulties.Contains(difficulty)) throw new ArgumentException("Unknown difficulty.");
        return Path.Combine(directory, difficulty + ".json");
    }
    public LabLayout? Load(string difficulty) => File.Exists(PathFor(difficulty)) ? LabImport.Parse(ReadFile(PathFor(difficulty))) : null;
    public static byte[] ReadFile(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length > LabImport.MaxBytes) throw new ArgumentException("Lab JSON exceeds the 1 MB limit.");
        var bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes); return bytes;
    }
    public LabLayout Save(byte[] bytes)
    {
        var layout = LabImport.Parse(bytes);
        Directory.CreateDirectory(directory);
        var target = PathFor(layout.Difficulty);
        var temporary = target + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return layout;
    }
}
