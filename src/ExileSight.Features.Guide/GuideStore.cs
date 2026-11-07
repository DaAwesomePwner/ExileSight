using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExileSight.Features.Guide;

public sealed record GuideSave(int Schema, string Version, int Index, long[] Times, bool Complete,
    bool Uncertain, bool LeagueStart, string Bandit, byte[]? Custom, string? CustomId = null, bool CustomDisabled = false);

/// <summary>One explicitly labelled default profile, never log-derived character identity.</summary>
public sealed class GuideStore(string directory)
{
    private readonly string path = Path.Combine(directory, "progress.json");
    private bool readOnly;
    private const int MaxBytes = 1500 * 1024;
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public string? Notice { get; private set; }
    public GuideSave? Load()
    {
        try
        {
            using var file = File.OpenRead(path);
            if (file.Length > MaxBytes) throw new JsonException();
            var state = JsonSerializer.Deserialize<GuideSave>(file, Options);
            if (state is null || state.Schema is not (1 or 2) || state.Version is null || state.Version.Length > 200 || state.Index is < 0 or > 2000 ||
                state.Times is not { Length: 11 } || state.Times.Any(t => t < 0 || t > 315360000000L) ||
                state.Bandit is not ("none" or "oak" or "alira" or "kraityn") || state.Custom?.Length > GuideImport.MaxBytes ||
                state.CustomId is not null && !ValidId(state.CustomId) ||
                state.Schema == 2 && (state.Custom is not null || file.Length > 8192)) throw new JsonException();
            return state;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        { readOnly = true; Notice = "Saved guide unavailable; file preserved read-only for this session."; return null; }
    }
    public void Preserve() { readOnly = true; Notice = "Incompatible saved guide preserved read-only for this session."; }
    private static bool ValidId(string id) => id.Length == 64 && id.All(char.IsAsciiHexDigit);
    public byte[] ReadCustom(string id)
    {
        if (!ValidId(id)) throw new ArgumentException("Invalid custom guide reference.");
        using var file = File.OpenRead(Path.Combine(directory, id + ".json"));
        if (file.Length > GuideImport.MaxBytes) throw new ArgumentException("Custom guide exceeds size limit.");
        var bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != id) throw new ArgumentException("Custom guide hash mismatch.");
        return bytes;
    }
    public string WriteCustom(byte[] bytes)
    {
        if (readOnly) throw new IOException("Progress is read-only; custom import was not saved.");
        if (bytes.Length > GuideImport.MaxBytes) throw new ArgumentException("Custom guide exceeds size limit.");
        var id = Convert.ToHexString(SHA256.HashData(bytes));
        var target = Path.Combine(directory, id + ".json");
        WriteAtomic(target, bytes);
        return id;
    }
    public void Save(GuideSave state)
    {
        if (readOnly) return;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Options);
        if (bytes.Length > MaxBytes) throw new IOException("Guide storage limit exceeded.");
        WriteAtomic(path, bytes);
        // Progress now references the committed immutable custom file; old imports are no longer needed.
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
            if (ValidId(Path.GetFileNameWithoutExtension(file)) && Path.GetFileNameWithoutExtension(file) != state.CustomId)
                File.Delete(file);
    }
    private void WriteAtomic(string target, byte[] bytes)
    {
        Directory.CreateDirectory(directory);
        var temp = target + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
