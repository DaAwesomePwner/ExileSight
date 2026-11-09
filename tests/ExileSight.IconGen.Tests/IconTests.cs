using ExileSight.IconGen;
using Xunit;

namespace ExileSight.IconGen.Tests;

public sealed class IconTests
{
    [Fact]
    public void CommittedIconMatchesGenerator() =>
        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "ExileSight.ico")), IconRenderer.Generate());

    [Fact]
    public void DirectoryAndBitmapFramesHaveAllSizesAnd32BitPixels()
    {
        var bytes = IconRenderer.Generate();
        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16());
        Assert.Equal(8, reader.ReadUInt16());
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];
        var expectedOffset = 6 + 8 * 16;
        foreach (var size in sizes)
        {
            Assert.Equal(size == 256 ? 0 : size, reader.ReadByte());
            Assert.Equal(size == 256 ? 0 : size, reader.ReadByte());
            Assert.Equal(0, reader.ReadUInt16());
            Assert.Equal(1, reader.ReadUInt16());
            Assert.Equal(32, reader.ReadUInt16());
            var length = reader.ReadInt32();
            var offset = reader.ReadInt32();
            Assert.Equal(expectedOffset, offset);
            Assert.Equal(40 + size * size * 4 + ((size + 31) / 32) * 4 * size, length);
            var nextEntry = reader.BaseStream.Position;
            reader.BaseStream.Position = offset;
            Assert.Equal(40, reader.ReadInt32());
            Assert.Equal(size, reader.ReadInt32());
            Assert.Equal(size * 2, reader.ReadInt32());
            Assert.Equal(1, reader.ReadUInt16());
            Assert.Equal(32, reader.ReadUInt16());
            Assert.Equal(0, reader.ReadInt32());
            reader.BaseStream.Position = nextEntry;
            expectedOffset += length;
        }
        Assert.Equal(bytes.Length, expectedOffset);
    }
}
