using System.Text.Json;
using ExileSight.Core.DataPacks;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class PackBytesTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new PackBytesConverter() } };

    [Fact]
    public void CompressedSnapshotPreservesCanonicalHashAndWireData()
    {
        var content = new PackContent("poe1/events", ExileSight.Core.Games.Game.PoE1, 3, "test", [], [],
            Events: new([new("event", "Unicode ✓ and \"quotes\"", true, "test")]));
        var expected = PackCodec.ContentBytes(content);
        var payload = new PackPayload(content);
        Assert.Equal(PackCodec.Hash(expected), payload.Hash);
        Assert.Equal(expected, PackCodec.ContentBytes(payload.CreateCopy()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(1024 * 1024)]
    public void CacheBytesRoundTripWithEveryPaddingLength(int length)
    {
        var bytes = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        var encoded = JsonSerializer.Serialize(bytes, Options);
        Assert.Equal(JsonSerializer.Serialize(bytes), encoded);
        Assert.Equal(bytes, JsonSerializer.Deserialize<byte[]>(encoded, Options));
    }

    [Theory]
    [InlineData("\"AQI=\"")]
    [InlineData("\"AQI\\u003d\"")]
    public void AcceptsStandardAndEscapedPadding(string json) =>
        Assert.Equal(new byte[] { 1, 2 }, JsonSerializer.Deserialize<byte[]>(json, Options));

    [Theory]
    [InlineData("\"!invalid\"")]
    [InlineData("\"=\"")]
    [InlineData("\"AA=\"")]
    [InlineData("123")]
    public void RejectsCorruptBytes(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<byte[]>(json, Options));
}
