using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ExileSight.Core.DataPacks;

/// <summary>Immutable compressed wire snapshot. Hash and compress while serializing, without a full JSON buffer.</summary>
internal sealed class PackPayload
{
    private readonly byte[] compressed;
    internal string Hash { get; }
    internal PackPayload(PackContent content)
    {
        using var buffer = new MemoryStream();
        using var hash = SHA256.Create();
        using (var compressor = new BrotliStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        using (var hashing = new CryptoStream(compressor, hash, CryptoStreamMode.Write))
        {
            // The async serializer drains bounded buffers; the synchronous overload buffers
            // an entire large object before writing. These streams are all in-memory.
            JsonSerializer.SerializeAsync(hashing, content, PackJson.Default.PackContent).GetAwaiter().GetResult();
            hashing.FlushFinalBlock();
            Hash = Convert.ToHexStringLower(hash.Hash!);
        }
        compressed = buffer.ToArray();
    }
    internal Stream Open() => new BrotliStream(new MemoryStream(compressed, writable: false), CompressionMode.Decompress);
    internal PackContent CreateCopy()
    {
        using var stream = Open();
        return JsonSerializer.Deserialize(stream, PackJson.Default.PackContent)!;
    }
    internal object Parse(string id)
    {
        using var stream = Open();
        return PackViews.Parse(stream, id);
    }
}
