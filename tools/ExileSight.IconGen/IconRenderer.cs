namespace ExileSight.IconGen;

/// <summary>Original ExileSight artwork, licensed under the repository's MIT license.</summary>
public static class IconRenderer
{
    public static ReadOnlySpan<int> Sizes => [16, 20, 24, 32, 40, 48, 64, 256];

    public static byte[] Generate()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)Sizes.Length);
        var offset = 6 + 16 * Sizes.Length;
        foreach (var size in Sizes)
        {
            var length = 40 + size * size * 4 + MaskStride(size) * size;
            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((ushort)0); // No palette; reserved byte zero.
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(length);
            writer.Write(offset);
            offset += length;
        }
        foreach (var size in Sizes) WriteFrame(writer, size);
        return stream.ToArray();
    }

    private static int MaskStride(int size) => ((size + 31) / 32) * 4;

    private static void WriteFrame(BinaryWriter writer, int size)
    {
        writer.Write(40);
        writer.Write(size);
        writer.Write(size * 2); // XOR bitmap plus AND mask.
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0); // BI_RGB
        writer.Write(size * size * 4);
        for (var i = 0; i < 4; i++) writer.Write(0);
        var pixels = Render(size);
        for (var y = size - 1; y >= 0; y--)
            writer.Write(pixels, y * size * 4, size * 4);
        for (var y = size - 1; y >= 0; y--)
        {
            var row = new byte[MaskStride(size)];
            for (var x = 0; x < size; x++)
                if (pixels[(y * size + x) * 4 + 3] == 0) row[x / 8] |= (byte)(128 >> (x % 8));
            writer.Write(row);
        }
    }

    /// <summary>Top-down straight-alpha BGRA pixels. Integer 8x8 coverage is platform independent.</summary>
    public static byte[] Render(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        var pixels = new byte[checked(size * size * 4)];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            int red = 0, green = 0, blue = 0, coverage = 0;
            for (var sy = 0; sy < 8; sy++)
            for (var sx = 0; sx < 8; sx++)
            {
                // Coordinates in [-1024, 1024]; no floating point or platform graphics APIs.
                var px = ((x * 8 + sx) * 2 + 1) * 1024 / (size * 8) - 1024;
                var py = ((y * 8 + sy) * 2 + 1) * 1024 / (size * 8) - 1024;
                var dx = Math.Max(Math.Abs(px) - 650, 0);
                var dy = Math.Max(Math.Abs(py) - 650, 0);
                if (dx * dx + dy * dy > 300 * 300) continue;
                coverage++;
                // A pointed lens bounded by two intersecting circles, with an open dark interior.
                var outer = px * px + (Math.Abs(py) + 480) * (Math.Abs(py) + 480) <= 850 * 850;
                var inner = px * px + (Math.Abs(py) + 480) * (Math.Abs(py) + 480) < 710 * 710;
                var iris = px * px + py * py <= 210 * 210;
                var pupil = px * px + py * py < 88 * 88;
                var gold = (outer && !inner) || (iris && !pupil);
                red += gold ? 246 : 22;
                green += gold ? 185 : 29;
                blue += gold ? 72 : 40;
            }
            var index = (y * size + x) * 4;
            if (coverage == 0) continue;
            pixels[index] = (byte)(blue / coverage);
            pixels[index + 1] = (byte)(green / coverage);
            pixels[index + 2] = (byte)(red / coverage);
            pixels[index + 3] = (byte)((coverage * 255 + 32) / 64);
        }
        return pixels;
    }
}
