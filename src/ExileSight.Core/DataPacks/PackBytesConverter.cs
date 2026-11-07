using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExileSight.Core.DataPacks;

/// <summary>Decode large cache envelopes without renting a pack-sized scratch buffer from ArrayPool.</summary>
public sealed class PackBytesConverter : JsonConverter<byte[]>
{
    public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected base64 pack bytes.");
        // Keep the standard JSON/base64 semantics for uncommon escaped or segmented input.
        if (reader.HasValueSequence || reader.ValueIsEscaped) return reader.GetBytesFromBase64();
        var encoded = reader.ValueSpan;
        var length = Base64.GetMaxDecodedFromUtf8Length(encoded.Length);
        if (encoded.EndsWith("="u8)) length--;
        if (encoded.EndsWith("=="u8)) length--;
        if (length < 0) throw new JsonException("Invalid base64 pack bytes.");
        var decoded = new byte[length];
        if (Base64.DecodeFromUtf8(encoded, decoded, out var consumed, out var written) != OperationStatus.Done || consumed != encoded.Length)
            throw new JsonException("Invalid base64 pack bytes.");
        if (written != decoded.Length) Array.Resize(ref decoded, written);
        return decoded;
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) =>
        writer.WriteBase64StringValue(value);
}
