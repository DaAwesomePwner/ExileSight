using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.DataPacks.Loading;

namespace ExileSight.DataGen;

public static class PackWriter
{
    public static async Task WriteAsync(string directory, DataPack[] packs)
    {
        Directory.CreateDirectory(directory);
        var sums = new List<string>();
        foreach (var pack in packs)
        {
            var name = PackLoader.AssetName(pack.Manifest.PackId);
            var outputs = new Dictionary<string, byte[]>
            {
                [name + ".json"] = PackCodec.ContentBytes(pack.Content),
                [name + ".manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(pack.Manifest, PackJson.Default.PackManifest),
                [name + ".pack.json"] = JsonSerializer.SerializeToUtf8Bytes(pack, PackJson.Default.DataPack)
            };
            foreach (var (file, bytes) in outputs)
            {
                await File.WriteAllBytesAsync(Path.Combine(directory, file), bytes);
                sums.Add($"{PackCodec.Hash(bytes)}  {file}");
            }
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "SHA256SUMS"), string.Join("\n", sums) + "\n");
    }
}
