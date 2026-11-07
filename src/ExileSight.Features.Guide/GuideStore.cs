using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExileSight.Features.Guide;

public sealed record GuideSave(int Schema, string Version, int Index, long[] Times, bool Complete,
    bool Uncertain, bool LeagueStart, string Bandit, byte[]? Custom);

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
            if (state is null || state.Schema != 1 || state.Version is null || state.Version.Length > 200 || state.Index is < 0 or > 2000 ||
                state.Times is not { Length: 11 } || state.Times.Any(t => t < 0 || t > 315360000000L) ||
                state.Bandit is not ("none" or "oak" or "alira" or "kraityn") || state.Custom?.Length > GuideImport.MaxBytes) throw new JsonException();
            return state;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        { readOnly = true; Notice = "Saved guide unavailable; file preserved read-only for this session."; return null; }
    }
    public void Preserve() { readOnly = true; Notice = "Incompatible saved guide preserved read-only for this session."; }
    public void Save(GuideSave state)
    {
        if (readOnly) return;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Options);
        if (bytes.Length > MaxBytes) throw new IOException("Guide storage limit exceeded.");
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
