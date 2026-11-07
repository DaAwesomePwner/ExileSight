using System.Text.Json;

namespace ExileSight.DataGen;

public static class DataGenConfiguration
{
    public static string SourceSha { get; } = ReadSourceSha();

    private static string ReadSourceSha()
    {
        using var stream = typeof(DataGenConfiguration).Assembly.GetManifestResourceStream("ExileSight.DataGen.repoe.json")
            ?? throw new InvalidOperationException("Missing embedded RePoE configuration.");
        using var config = JsonDocument.Parse(stream);
        var sha = config.RootElement.GetProperty("sourceSha").GetString()
            ?? throw new InvalidOperationException("Missing pinned source SHA.");
        RepoeConverter.ValidateSha(sha);
        return sha.ToLowerInvariant();
    }
}
